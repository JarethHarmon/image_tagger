using Godot;
using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using Alphaleonis.Win32.Filesystem;
using CoenM.ImageHash;
using CoenM.ImageHash.HashAlgorithms;
using LiteDB;
using Data;
using ImageMagick;

public class Database : Node
{
/*==============================================================================*/
/*                                   Variables                                  */
/*==============================================================================*/
	private int progressSectionSize = 16;
	private string metadataPath;
	public void SetMetadataPath(string path) { metadataPath = path; }

	/* may eventually reduce and merge these (especially merging dbGroups with dbTags) */
	private LiteDatabase dbHashes, dbImports, dbGroups, dbTags;
	private ILiteCollection<HashInfo> colHashes;
	private ILiteCollection<ImportInfo> colImports;
	private ILiteCollection<ImportProgress> colProgress;
	private ILiteCollection<GroupInfo> colGroups;
	private ILiteCollection<TagInfo> colTags;
	private ILiteCollection<TabInfo> colTabs;

	/* for thread safety, it might be better to make all of these into concurrent dictionaries */
	private Dictionary<string, HashInfo> dictHashes = new Dictionary<string, HashInfo>();
	private ConcurrentDictionary<string, ImportInfo> dictImports = new ConcurrentDictionary<string, ImportInfo>();
	private Dictionary<string, GroupInfo> dictGroups = new Dictionary<string, GroupInfo>();
	private Dictionary<string, TagInfo> dictTags = new Dictionary<string, TagInfo>();
	private Dictionary<string, TabInfo> dictTabs = new Dictionary<string, TabInfo>();

	private ImageScanner scanner;
	private ImageImporter importer;
	private Node globals, signals;

/*==============================================================================*/
/*                                 Initialization                               */
/*==============================================================================*/
	public override void _Ready()
	{
		scanner = (ImageScanner) GetNode("/root/ImageScanner");
		importer = (ImageImporter) GetNode("/root/ImageImporter");
		globals = (Node) GetNode("/root/Globals");
		signals = (Node) GetNode("/root/Signals");
	}

	public int Create()
	{
		try {
			dbHashes = new LiteDatabase(metadataPath + "hash_info.db");
			dbImports = new LiteDatabase(metadataPath + "import_info.db");
			dbGroups = new LiteDatabase(metadataPath + "group_info.db");
			dbTags = new LiteDatabase(metadataPath + "tag_info.db");

			BsonMapper.Global.Entity<HashInfo>().Id(x => x.imageHash);
			BsonMapper.Global.Entity<ImportInfo>().Id(x => x.importId);
			BsonMapper.Global.Entity<ImportProgress>().Id(x => x.progressId);
			BsonMapper.Global.Entity<GroupInfo>().Id(x => x.groupId);
			BsonMapper.Global.Entity<TagInfo>().Id(x => x.tagId);
			BsonMapper.Global.Entity<TabInfo>().Id(x => x.tabId);

			colHashes = dbHashes.GetCollection<HashInfo>("hashes");
			colImports = dbImports.GetCollection<ImportInfo>("imports");
			colProgress = dbImports.GetCollection<ImportProgress>("progress");
			colGroups = dbGroups.GetCollection<GroupInfo>("groups");
			colTags = dbTags.GetCollection<TagInfo>("tags");
			colTabs = dbImports.GetCollection<TabInfo>("tabs");

			colHashes.EnsureIndex(x => x.imageHash);
			colHashes.EnsureIndex(x => x.colorHash[0] + x.colorHash[15] + x.colorHash[7]);
			colHashes.EnsureIndex(x => x.imports);
			colHashes.EnsureIndex(x => x.groups);
			colHashes.EnsureIndex(x => x.tags);
			colHashes.EnsureIndex(x => x.ratings["Sum"]);
			colHashes.EnsureIndex(x => x.uploadTime);

			//colHashes.EnsureIndex(x => x.numColors);
			//colHashes.EnsureIndex(x => x.size);
			//colHashes.EnsureIndex(x => x.width*x.height);
			//colHashes.EnsureIndex(x => x.tags.Count);
			//colHashes.EnsureIndex(x => x.creationTime);
			//colHashes.EnsureIndex(x => x.lastWriteTime);
			//colHashes.EnsureIndex(x => x.imageName);
			//colHashes.EnsureIndex(x => x.paths.FirstOrDefault());
			//colHashes.EnsureIndex(x => x.ratings["Quality"]);
			//colHashes.EnsureIndex(x => x.ratings["Appeal"]);
			//colHashes.EnsureIndex(x => x.ratings["Art"]);
			//colHashes.EnsureIndex(x => x.ratings["Average"]);			

			return (int)ErrorCodes.OK;
		} 
		catch (Exception ex) {
			GD.Print("Database::Create() : ", ex); 
			return (int)ErrorCodes.ERROR; 
		}
	} 
	
	public void CreateAllInfo() 
	{
		try {
			var allInfo = colImports.FindById("All");
			if (allInfo == null) {
				allInfo = new ImportInfo {
					importId = "All",
					success = 0,
					failed = 0,
					total = 0,
					finished = true,
					importStart = 0,
					importFinish = 0,
				};
				colImports.Insert(allInfo);
			}
			AddImport("All", allInfo);
			var tabInfo = colTabs.FindById("All");
			if (tabInfo == null) {
				tabInfo = new TabInfo {
					tabId = "All",
					tabType = 0, 
					tabName = "All",
					importId = "All",
				};
				colTabs.Insert(tabInfo);
			}
			dictTabs["All"] = tabInfo;
		} 
		catch(Exception ex) { 
			GD.Print("Database::CreateAllInfo() : ", ex);
			return;
		}
	}

	public void LoadImportDb()
	{
		try {
			var imports = colImports.FindAll();
			foreach (ImportInfo importInfo in imports)
				AddImport(importInfo.importId, importInfo);
			var tabs = colTabs.FindAll();
			foreach (TabInfo tabInfo in tabs)
				dictTabs[tabInfo.tabId] = tabInfo;
		}
		catch (Exception ex) {
			GD.Print("Database::LoadImportDb() : ", ex);
			return;
		}
	}

	public void Destroy()
	{
		dbHashes.Dispose();
		dbImports.Dispose();
		dbGroups.Dispose();
		dbTags.Dispose();
	}

	public void CheckpointHashDB() { dbHashes.Checkpoint(); }
	public void CheckpointImportDB() { dbImports.Checkpoint(); }
	public void CheckpointGroupDB() { dbGroups.Checkpoint(); }
	public void CheckpointTagDB() { dbTags.Checkpoint(); }

/*==============================================================================*/
/*                            Temporary Hash Storage                            */
/*==============================================================================*/
	// { importId : { imageHash : hashInfo }}
	private Dictionary<string, Dictionary<string, HashInfo>> tempHashInfo = new Dictionary<string, Dictionary<string, HashInfo>>();
	private Dictionary<string, HashSet<string>> tempHashes = new Dictionary<string, HashSet<string>>();
	
	private void MergeHashSets(HashSet<string> main, HashSet<string> sub)
	{
		if (sub.Count == 0) return;
		if (main.Count == 0) main = sub;
		foreach (string member in sub)
			main.Add(member);
	}
	
	public void MergeHashInfo(HashInfo main, HashInfo sub)
	{
		if (main.paths != null && sub.paths != null) MergeHashSets(main.paths, sub.paths);
		else if (sub.paths != null) main.paths = sub.paths; 

		if (main.imports != null && sub.imports != null) MergeHashSets(main.imports, sub.imports);
		else if (sub.imports != null) main.imports = sub.imports; 

		if (main.tags != null && sub.tags != null) MergeHashSets(main.tags, sub.tags);
		else if (sub.tags != null) main.tags = sub.tags; 
	}

	public void StoreOneTempHashInfo(string importId, string progressId, HashInfo hashInfo)
	{
		lock (tempHashInfo) {
			if (!tempHashInfo.ContainsKey(importId)) tempHashInfo[importId] = new Dictionary<string, HashInfo>();
			if (!tempHashes.ContainsKey(progressId)) tempHashes[progressId] = new HashSet<string>();
			if (tempHashInfo[importId].ContainsKey(hashInfo.imageHash)) {
				var _hashInfo = tempHashInfo[importId][hashInfo.imageHash];
				MergeHashInfo(hashInfo, _hashInfo);
			}
			tempHashInfo[importId][hashInfo.imageHash] = hashInfo;
			tempHashes[progressId].Add(hashInfo.imageHash);
		}
	}

	public void StoreTempHashInfo(string importId, string progressId, List<HashInfo> sectionData, int[] results)
	{
		if (sectionData.Count < 1) return;
		// check if in current importId or if in database already
		lock (tempHashInfo) {
			// iterate over each HashInfo in this section
			if (!tempHashInfo.ContainsKey(importId)) tempHashInfo[importId] = new Dictionary<string, HashInfo>();
			if (!tempHashes.ContainsKey(progressId)) tempHashes[progressId] = new HashSet<string>();
			foreach (HashInfo hashInfo in sectionData) {
				// if tempHashInfo already contains this hash in the current import, merge it with the new one
				if (tempHashInfo[importId].ContainsKey(hashInfo.imageHash)) {
					var _hashInfo = tempHashInfo[importId][hashInfo.imageHash];
					MergeHashInfo(hashInfo, _hashInfo);
				}
				// upsert this hashInfo into tempHashInfo and tempHashes
				tempHashInfo[importId][hashInfo.imageHash] = hashInfo;
				tempHashes[progressId].Add(hashInfo.imageHash);
			}
			// update the results counts in the imports dictionary
			/*if (results.Length < 4) return;
			var importInfo = GetImport(importId);
			importInfo.success += results[0];
			importInfo.duplicate += results[1];
			importInfo.ignored += results[2];
			importInfo.failed += results[3];
			AddImport(importId, importInfo);*/
		}
	}
	
	public HashInfo GetHashInfo(string importId, string imageHash)
	{
		lock (tempHashInfo) { 
			if (tempHashInfo.ContainsKey(importId)) 
				if (tempHashInfo[importId].ContainsKey(imageHash))
					return tempHashInfo[importId][imageHash]; 
		}
		if (dictHashes.ContainsKey(imageHash)) return dictHashes[imageHash];
		return colHashes.FindById(imageHash);
	}
	
	public bool IsIgnored(string importId, string imageHash)
	{
		var hashInfo = GetHashInfo(importId, imageHash);
		if (hashInfo == null) return false;
		if (hashInfo.imports == null) return false;
		if (hashInfo.imports.Contains(importId)) return true;
		return false;
	}
	
	public bool IsDuplicate(string importId, string imageHash)
	{
		foreach (string iid in tempHashInfo.Keys)
			if (!iid.Equals(importId))
				if (GetHashInfo(iid, imageHash) != null)
					return true;
		var temp = colHashes.FindById(imageHash);
		if (temp != null)
			if (temp.imports != null)
				if (!temp.imports.Contains(importId))
					return true;
		return false;		
	}

	public HashInfo GetDictHashInfo(string imageHash)
	{
		if (dictHashes.ContainsKey(imageHash)) return dictHashes[imageHash];
		return null;
	}

	public void UpsertDictHashInfo(string imageHash, HashInfo hashInfo)
	{
		dictHashes[imageHash] = hashInfo;
	}

/*==============================================================================*/
/*                                  Hash Database                               */
/*==============================================================================*/


	
	private int _lastQueriedCount = 0;
	public int GetLastQueriedCount() { return _lastQueriedCount; }
	
	public void AddRating(string imageHash, string ratingName, int ratingValue)
	{
		try {
			if (dictHashes.ContainsKey(imageHash)) {
				if (dictHashes[imageHash].ratings == null) 
					dictHashes[imageHash].ratings = new Dictionary<string, int>();
				dictHashes[imageHash].ratings[ratingName] = ratingValue;
			}
			var hashInfo = colHashes.FindById(imageHash);
			if (hashInfo == null) return;
			if (hashInfo.ratings == null) 
				hashInfo.ratings =	new Dictionary<string, int>();
			hashInfo.ratings[ratingName] = ratingValue;
			int sum = 0, count = 0;
			foreach (string _name in hashInfo.ratings.Keys) {
				if (!_name.Equals("Sum") && !_name.Equals("Average")) {
					sum += hashInfo.ratings[_name];
					count++;
				}
			}
			hashInfo.ratings["Sum"] = sum;
			hashInfo.ratings["Average"] = (int)Math.Round((double)sum/count);
			colHashes.Update(hashInfo);
		} catch (Exception ex) { GD.Print("Database::AddRating() : ", ex); return; }		
	}
	public int GetRating(string imageHash, string ratingName)
	{
		try {
			if (dictHashes.ContainsKey(imageHash)) 
				if (dictHashes[imageHash].ratings != null)
					return (dictHashes[imageHash].ratings.ContainsKey(ratingName)) ?
						dictHashes[imageHash].ratings[ratingName] : 0;
			return 0;
		} catch (Exception ex) { GD.Print("Database::GetRating() : ", ex); return 0; }	
	}
	
	public void PopulateDictHashes(string[] imageHashes)
	{
		try {
			//var now = DateTime.Now;
			dictHashes.Clear();
			foreach (string imageHash in imageHashes) {
				var hashInfo = colHashes.FindById(imageHash);
				dictHashes[imageHash] = hashInfo;
			}
			//GD.Print(DateTime.Now-now);
		} catch (Exception ex) { GD.Print("Database::PopulateDictHashes(): ", ex); return; }
	}

	public int GetFileType(string imageHash) 
	{
		return dictHashes.ContainsKey(imageHash) ? dictHashes[imageHash].thumbnailType : -1;
	}
	// returning long would be better, but godot does not marshal long into its 64bit integer type for some reason
	// instead return as string, convert with .to_int() and convert to size with String.humanize_size(int)
	public string GetFileSize(string imageHash) 
	{
		return dictHashes.ContainsKey(imageHash) ? dictHashes[imageHash].size.ToString() : "";
	}
	
	public string[] GetHashPaths(string imageHash)
	{
		try {
			if (dictHashes.ContainsKey(imageHash))
				return dictHashes[imageHash].paths.ToArray();
			var result = colHashes.FindById(imageHash);
			if (result == null) return new string[0];
			return result.paths.ToArray();
		} catch (Exception ex) { return new string[0]; }
	}
	
	public string GetImageName(string imageHash)
	{
		try {
			if (dictHashes.ContainsKey(imageHash))
				return (dictHashes[imageHash].imageName == null) ? "" : dictHashes[imageHash].imageName;
			var result = colHashes.FindById(imageHash);
			if (result == null) return "";
			return (result.imageName == null) ? "" : result.imageName;
		} catch (Exception ex) { return ""; }
	}

	public string[] GetTags(string imageHash)
	{
		try {
			if (dictHashes.ContainsKey(imageHash))
				return dictHashes[imageHash].tags.ToArray();
			var result = colHashes.FindById(imageHash);
			if (result == null) return new string[0];
			return result.tags.ToArray();
		} catch (Exception ex) { return new string[0]; }
	}
	
	public string GetDiffHash(string imageHash)
	{
		try {
			if (dictHashes.ContainsKey(imageHash))
				return dictHashes[imageHash].differenceHash.ToString();
			return "";
		} catch { return ""; }
	}
	public float[] GetColorHash(string imageHash) 
	{
		try {
			if (dictHashes.ContainsKey(imageHash))
				return dictHashes[imageHash].colorHash;
			return new float[0];
		} catch { GD.Print("ERR"); return new float[0]; }
	}
	public string GetCreationTime(string imageHash)
	{
		try {
			if (dictHashes.ContainsKey(imageHash))
				return  new DateTime(dictHashes[imageHash].creationTime).ToString();
			return "";
		} catch { return ""; }
	}

	public string[] QueryDatabase(string tabId, int offset, int count, string[] tagsAll, string[] tagsAny, string[] tagsNone, int sort=(int)Sort.SHA256, int order=(int)Order.ASCENDING, bool countResults=false, int similarity=(int)Similarity.AVERAGE)
	{
		try {			
			dictHashes.Clear();
			var results = new List<string>();
			int tabType = GetTabType(tabId);
			if (tabType == (int)Tab.IMPORT_GROUP) {
				string importId = GetImportId(tabId);
				var hashInfos = _QueryImport(importId, offset, count, tagsAll, tagsAny, tagsNone, sort, order, countResults);
				if (hashInfos == null) return new string[0];
				foreach (HashInfo hashInfo in hashInfos) {
					results.Add(hashInfo.imageHash);
					dictHashes[hashInfo.imageHash] = hashInfo;
				}
			}
			// image group
			// tag
			else if (tabType == (int)Tab.SIMILARITY) {
				string imageHash = GetSimilarityHash(tabId);
				var temp = colHashes.FindById(imageHash);
				if (temp == null) return new string[0];
				//var hashInfos = _QueryBySimilarity("All", temp.colorHash, temp.differenceHash, offset, count, tagsAll, tagsAny, tagsNone, similarity); 
				var hashInfos = _QueryBySimilarity("All", temp.colorHash, temp.differenceHash, temp.perceptualHash, offset, count, tagsAll, tagsAny, tagsNone, similarity); 

				if (hashInfos == null) return new string[0];

				foreach (HashInfo hashInfo in hashInfos) {
					results.Add(hashInfo.imageHash);
					dictHashes[hashInfo.imageHash] = hashInfo;
				}
			}
			return results.ToArray();
		} catch (Exception ex) { GD.Print("Database::QueryDatabase() : ", ex); return new string[0]; }
	}

	private List<HashInfo> _QueryImport(string importId, int offset, int count, string[] tagsAll, string[] tagsAny, string[] tagsNone, int sort=(int)Sort.SHA256, int order=(int)Order.ASCENDING, bool countResults=false)
	{
		try {
			var now = DateTime.Now;
			bool counted=false;

			if (tagsAll.Length == 0 && tagsAny.Length == 0 && tagsNone.Length == 0) {
				_lastQueriedCount = (importId.Equals("All")) ? GetSuccessCount(importId) : GetSuccessOrDuplicateCount(importId);
				counted = true;
			}

			var rng = new Random(); // for SortBy.Random (if I can figure out how to do so)
			var query = colHashes.Query();

			if (importId != "All") query = query.Where(x => x.imports.Contains(importId));
			//if (groupId != "") query = query.Where(x => x.groups.Contains(groupId));
			
			if (tagsAll.Length > 0) foreach (string tag in tagsAll) query = query.Where(x => x.tags.Contains(tag));
			if (tagsAny.Length > 0) query = query.Where("$.tags ANY IN @0", BsonMapper.Global.Serialize(tagsAny));
			if (tagsNone.Length > 0) foreach (string tag in tagsNone) query = query.Where(x => !x.tags.Contains(tag));
			
			if (countResults && !counted) _lastQueriedCount = query.Count(); // slow

			if (sort == (int)Sort.SIZE) query = (order == (int)Order.ASCENDING) ? query.OrderBy(x => x.size) : query.OrderByDescending(x => x.size);
			else if (sort == (int)Sort.PATH) query = (order == (int)Order.ASCENDING) ? query.OrderBy(x => x.paths.FirstOrDefault()) : query.OrderByDescending(x => x.paths.FirstOrDefault());
			else if (sort == (int)Sort.NAME) query = (order == (int)Order.ASCENDING) ? query.OrderBy(x => x.imageName) : query.OrderByDescending(x => x.imageName);
			else if (sort == (int)Sort.CREATION_TIME) query = (order == (int)Order.ASCENDING) ? query.OrderBy(x => x.creationTime) : query.OrderByDescending(x => x.creationTime);
			else if (sort == (int)Sort.UPLOAD_TIME) query = (order == (int)Order.ASCENDING) ? query.OrderBy(x => x.uploadTime) : query.OrderByDescending(x => x.uploadTime);
			else if (sort == (int)Sort.EDIT_TIME) query = (order == (int)Order.ASCENDING) ? query.OrderBy(x => x.lastWriteTime) : query.OrderByDescending(x => x.lastWriteTime);
			else if (sort == (int)Sort.DIMENSIONS) query = (order == (int)Order.ASCENDING) ? query.OrderBy(x => x.width*x.height) : query.OrderByDescending(x => x.width*x.height);
			else if (sort == (int)Sort.TAG_COUNT) query = (order == (int)Order.ASCENDING) ? query.OrderBy(x => x.tags.Count) : query.OrderByDescending(x => x.tags.Count);
			else if (sort == (int)Sort.IMAGE_COLOR) query = (order == (int)Order.ASCENDING) ? query.OrderBy(x => x.colorHash[0] + x.colorHash[15] + x.colorHash[7]) : query.OrderByDescending(x => x.colorHash[0] + x.colorHash[15] + x.colorHash[7]);
			//else if (sort == (int)Sort.IMAGE_COLOR) query = (order == (int)Order.ASCENDING) ? query.OrderBy(x => x.numColors) :  query.OrderByDescending(x => x.numColors);

			else if (sort == (int)Sort.RANDOM) query = (order == (int)Order.ASCENDING) ? query.OrderBy(_ => Guid.NewGuid()) : query.OrderByDescending(_ => Guid.NewGuid());
			// need a way to handle this that allows custom user ratings
			else if (sort == (int)Sort.RATING_QUALITY) query = (order == (int)Order.ASCENDING) ? query.OrderBy(x => x.ratings["Quality"]) : query.OrderByDescending(x => x.ratings["Quality"]);
			else if (sort == (int)Sort.RATING_APPEAL) query = (order == (int)Order.ASCENDING) ? query.OrderBy(x => x.ratings["Appeal"]) : query.OrderByDescending(x => x.ratings["Appeal"]);
			else if (sort == (int)Sort.RATING_ART_STYLE) query = (order == (int)Order.ASCENDING) ? query.OrderBy(x => x.ratings["Art"]) : query.OrderByDescending(x => x.ratings["Art"]);
			else if (sort == (int)Sort.RATING_SUM) query = (order == (int)Order.ASCENDING) ? query.OrderBy(x => x.ratings["Sum"]) : query.OrderByDescending(x => x.ratings["Sum"]);
			else if (sort == (int)Sort.RATING_AVERAGE) query = (order == (int)Order.ASCENDING) ? query.OrderBy(x => x.ratings["Average"]) : query.OrderByDescending(x => x.ratings["Average"]);			
			else query = (order == (int)Order.ASCENDING) ? query.OrderBy(x => x.imageHash) : query.OrderByDescending(x => x.imageHash);

			/* only current hope for supporting tags formatted as
					[[A,B],[C,D]]  :ie:  (A && B) || (C && D) 
				is PredicateBuilder (which is slower I believe) */

			var list = query.Offset(offset).Limit(count).ToList();
			return list;
		} 
		catch (Exception ex) { 
			GD.Print("Database::_QueryDatabase() : ", ex); 
			return null; 
		}
	}

	private class SimilarityQueryResult
	{
		public string imageHash { get; set; }
		public float[] colorHash { get; set; }
		public ulong differenceHash { get; set; }
		public float similarity { get; set; }
		public string perceptualHash { get; set; }
	}

	/*public List<HashInfo> _QueryBySimilarity(string importId, float[] colorHash, ulong differenceHash, int offset, int count, string[] tagsAll, string[] tagsAny, string[] tagsNone, int similarityMode=(int)Similarity.AVERAGE)
	{
		var result1 = _QueryFilteredSimilarity(importId, tagsAll, tagsAny, tagsNone, similarityMode);
		
		if (similarityMode == (int)Similarity.AVERAGE)
			foreach (SimilarityQueryResult result in result1)
				result.similarity = (ColorSimilarity(result.colorHash, colorHash) + (float)DifferenceSimilarity(result.differenceHash, differenceHash)) / 2f;
		else if (similarityMode == (int)Similarity.COLOR)
			foreach (SimilarityQueryResult result in result1)
				result.similarity = ColorSimilarity(result.colorHash, colorHash);
		else
			foreach (SimilarityQueryResult result in result1)
				result.similarity = (float)DifferenceSimilarity(result.differenceHash, differenceHash);
		
		var result2 = result1.OrderByDescending(x => x.similarity).Skip(offset).Take(count);
		result1 = null;
		
		var result3 = new List<HashInfo>();
		foreach (SimilarityQueryResult result in result2)
			result3.Add(colHashes.FindById(result.imageHash));
	
		result2 = null;

		return result3.ToList();
	}*/
	
	public List<HashInfo> _QueryBySimilarity(string importId, float[] colorHash, ulong differenceHash, string perceptualHash, int offset, int count, string[] tagsAll, string[] tagsAny, string[] tagsNone, int similarityMode=(int)Similarity.AVERAGE)
	{
		var result1 = _QueryFilteredSimilarity(importId, tagsAll, tagsAny, tagsNone, similarityMode);
		
		if (similarityMode == (int)Similarity.AVERAGE)
			foreach (SimilarityQueryResult result in result1)
				result.similarity = (ColorSimilarity(result.colorHash, colorHash) + 
					(float)PerceptualSimilarity(result.perceptualHash, perceptualHash) + 
					(float)DifferenceSimilarity(result.differenceHash, differenceHash)) / 3f;
		else if (similarityMode == (int)Similarity.COLOR)
			foreach (SimilarityQueryResult result in result1)
				result.similarity = ColorSimilarity(result.colorHash, colorHash);
		else if (similarityMode == (int)Similarity.PERCEPTUAL)
			foreach (SimilarityQueryResult result in result1)
				result.similarity = (float)PerceptualSimilarity(result.perceptualHash, perceptualHash);
		else
			foreach (SimilarityQueryResult result in result1)
				result.similarity = (float)DifferenceSimilarity(result.differenceHash, differenceHash);
		
		var result2 = result1.OrderByDescending(x => x.similarity).Skip(offset).Take(count);
		result1 = null;
		
		var result3 = new List<HashInfo>();
		foreach (SimilarityQueryResult result in result2)
			result3.Add(colHashes.FindById(result.imageHash));
	
		result2 = null;

		return result3.ToList();
	}

	private List<SimilarityQueryResult> _QueryFilteredSimilarity(string importId, string[] tagsAll, string[] tagsAny, string[] tagsNone, int similarityMode)
	{
		var query = colHashes.Query();

		if (importId != "All") query = query.Where(x => x.imports.Contains(importId));
		if (tagsAll.Length > 0) foreach (string tag in tagsAll) query = query.Where(x => x.tags.Contains(tag));
		if (tagsAny.Length > 0) query = query.Where("$.tags ANY IN @0", BsonMapper.Global.Serialize(tagsAny));
		if (tagsNone.Length > 0) foreach (string tag in tagsNone) query = query.Where(x => !x.tags.Contains(tag));

		if (similarityMode == (int)Similarity.AVERAGE)
			return query.Select(x => new SimilarityQueryResult { 
				imageHash = x.imageHash, 
				colorHash = x.colorHash,
				differenceHash = x.differenceHash,
				perceptualHash = x.perceptualHash,
			}).ToList();
		else if (similarityMode == (int)Similarity.COLOR)
			return query.Select(x => new SimilarityQueryResult { 
				imageHash = x.imageHash, 
				colorHash = x.colorHash,
			}).ToList();
		else if (similarityMode == (int)Similarity.PERCEPTUAL)
			return query.Select(x => new SimilarityQueryResult { 
				imageHash = x.imageHash, 
				perceptualHash = x.perceptualHash,
			}).ToList(); 
		else
			return query.Select(x => new SimilarityQueryResult { 
				imageHash = x.imageHash, 
				differenceHash = x.differenceHash,
			}).ToList();
	}

	public void BulkAddTags(string[] imageHashes, string[] tags)
	{
		try {
			var list = new List<HashInfo>();
			foreach (string imageHash in imageHashes) {
				var tmp = colHashes.FindById(imageHash);
				if (tmp == null) continue;
				if (tmp.tags == null) tmp.tags = new HashSet<string>(tags);
				else foreach (string tag in tags) tmp.tags.Add(tag);
				if (dictHashes.ContainsKey(imageHash)) dictHashes[imageHash] = tmp;
				list.Add(tmp);
			}
			colHashes.Update(list);
		} 
		catch (Exception ex) { 
			GD.Print("Database::BulkAddTags() : ", ex); 
			return; 
		}
	}
	
	public void BulkRemoveTags(string[] imageHashes, string[] tags)
	{
		try {
			var list = new List<HashInfo>();
			foreach (string imageHash in imageHashes) {
				var tmp = colHashes.FindById(imageHash);
				if (tmp == null) continue;
				if (tmp.tags == null) continue;
				foreach (string tag in tags) tmp.tags.Remove(tag);
				if (tmp.tags.Count == 0) tmp.tags = null;
				if (dictHashes.ContainsKey(imageHash)) dictHashes[imageHash] = tmp;
				list.Add(tmp);
			}
			colHashes.Update(list);
		} 
		catch (Exception ex) { 
			GD.Print("Database::BulkRemoveTags() : ", ex); 
			return; 
		}
	}

/*==============================================================================*/
/*                                 Import Database                              */
/*==============================================================================*/
	public void CommitImport(string importId, string importName)
	{
		string[] paths = scanner.GetPaths();
		CreateImport(importId, importName, paths.Length, paths);
	}
	public void CreateImport(string _id, string _name, int _total, string[] _paths)
	{
		try {
			if (_total == 0) return;
			var importInfo = new ImportInfo {
				importId = _id,
				importName = _name,
				total = _total,
				processed = 0,
				success = 0,
				ignored = 0,
				duplicate = 0,
				failed = 0,
				importStart = DateTime.Now.Ticks,
				importFinish = 0,
				finished = false,
				progressIds = new HashSet<string>(),
			};

			int numSections = (int)Math.Ceiling((double)_total/progressSectionSize);
			int lastSectionSize = _total-((numSections-1) * progressSectionSize);
			var listProgress = new List<ImportProgress>();

			for (int i = 0; i < numSections-1; i++) {
				string[] __paths = new string[progressSectionSize];
				Array.Copy(_paths, i * progressSectionSize, __paths, 0, progressSectionSize);

				var _progressId = importer.CreateProgressID();
				var importProgress = new ImportProgress {
					progressId = _progressId,
					paths = __paths,
				};
				listProgress.Add(importProgress);
				importInfo.progressIds.Add(_progressId);
			}
			if (lastSectionSize > 0) {
				string[] __paths = new string[lastSectionSize];
				Array.Copy(_paths, _total-lastSectionSize, __paths, 0, lastSectionSize);

				var _progressId = importer.CreateProgressID();
				var importProgress = new ImportProgress {
					progressId = _progressId,
					paths = __paths,
				};
				listProgress.Add(importProgress);
				importInfo.progressIds.Add(_progressId);
			}
			AddImport(_id, importInfo);
			dbImports.BeginTrans();
			colImports.Insert(importInfo);
			foreach (ImportProgress imp in listProgress)
				colProgress.Insert(imp);
			dbImports.Commit();
		} 
		catch (Exception ex) { 
			GD.Print("Database::CreateImport() : ", ex); 
			return; 
		}
	}

	public string[] GetTabIDs(string importId)
	{
		try {
			if (importId.Equals("")) 
				return new string[0];
			var query = colTabs.Query();
			query = query.Where(x => x.importId.Equals(importId));
			var tabs = new List<string>();
			foreach (TabInfo tinfo in query.ToEnumerable())
				tabs.Add(tinfo.tabId);
			return tabs.ToArray();
		} 
		catch (Exception ex) { 
			GD.Print("Database::GetTabIDs() : ", ex); 
			return new string[0]; 
		}
	}
	
	public string[] GetPaths(string progressId)
	{
		try {
			var importProgress = colProgress.FindById(progressId);
			if (importProgress == null) return new string[0];
			if (importProgress.paths == null) return new string[0];
			return importProgress.paths;
		}
		catch (Exception ex) { 
			GD.Print("Database::GetPaths() : ", ex); 
			return new string[0]; 
		} 
	}

	public void FinishImport(string importId)
	{
		try {
			var importInfo = GetImport(importId);
			if (importInfo == null) return;
			importInfo.finished = true;
			importInfo.importFinish = DateTime.Now.Ticks;
			AddImport(importId, importInfo);
			colImports.Update(importInfo);
			dictImports[importId] = importInfo; // forgot to update dictionary which was causing issues
			importer.ignoredChecker.Remove(importId);
		}
		catch (Exception ex) {
			GD.Print("Database::FinishImport() : ", ex);
			return;
		}
	}

	private static readonly object locker = new object();
	public void FinishImportSection(string importId, string progressId)
	{
		
		if (!tempHashes.ContainsKey(progressId)) return;
		string[] hashes = tempHashes[progressId].ToArray();

		var hashInfoList = new List<HashInfo>();
		foreach (string hash in hashes) {
			HashInfo hashInfo = null;
			lock (tempHashInfo) { 
				if (tempHashInfo.ContainsKey(importId)) 
					if (tempHashInfo[importId].ContainsKey(hash))
						hashInfo = tempHashInfo[importId][hash];
			}
			if (hashInfo == null) continue;

			var dbHashInfo = colHashes.FindById(hash);
			if (dbHashInfo != null) MergeHashInfo(hashInfo, dbHashInfo);
			hashInfoList.Add(hashInfo);
		}
		colHashes.Upsert(hashInfoList);

		lock (locker) {
			var importInfo = GetImport(importId);
			var allInfo = GetImport("All");

			importInfo.progressIds.Remove(progressId);
			colImports.Update(importInfo);
			colImports.Update(allInfo);
			colProgress.Delete(progressId);
			tempHashes.Remove(progressId);

			if (importInfo.progressIds.Count == 0) {
				string[] tabs = GetTabIDs(importId);
				FinishImport(importId);
				signals.Call("emit_signal", "finish_import_buttons", tabs);
			}
		}

		foreach (HashInfo hashInfo in hashInfoList) {
			if (hashInfo.imports == null) hashInfo.imports = new HashSet<string>();
			hashInfo.imports.Add(importId);
		}
		colHashes.Upsert(hashInfoList);
		lock (locker) {
			foreach (HashInfo hashInfo in hashInfoList)
				if (importer.ignoredChecker.ContainsKey(importId))
					importer.ignoredChecker[importId].Remove(hashInfo.imageHash);
		}
		//GD.Print("finished: ", progressId);
	}

/*==============================================================================*/
/*                                Import Dictionary                             */
/*==============================================================================*/
	/* inserts an importinfo into dictImports */
	public void AddImport(string importId, ImportInfo importInfo)
	{
		bool result = dictImports.TryAdd(importId, importInfo);
		if (!result) {
			ImportInfo temp;
			result = dictImports.TryGetValue(importId, out temp);
			result = dictImports.TryUpdate(importId, importInfo, temp);
		}
	}

	public void UpdateImportCounts(string importId, int[] results)
	{
		try {
			var importInfo = GetImport(importId);
			var allInfo = GetImport("All");

			allInfo.success += results[(int)ImportCode.SUCCESS];
			importInfo.success += results[(int)ImportCode.SUCCESS];
			importInfo.duplicate += results[(int)ImportCode.DUPLICATE];
			importInfo.ignored += results[(int)ImportCode.IGNORED];
			importInfo.failed += results[(int)ImportCode.FAILED];
			importInfo.processed += results[0] + results[1] + results[2] + results[3];

			AddImport("All", allInfo);
			AddImport(importId, importInfo);
		} 
		catch(Exception ex) { 
			GD.Print("Database::UpdateImportCounts() : ", ex); 
			return; 
		}
	}

	public ImportInfo GetImport(string importId)
	{
		ImportInfo importInfo = null;
		dictImports.TryGetValue(importId, out importInfo);
		return importInfo;
	}

	public string[] GetProgressIds(string importId)
	{
		var importInfo = GetImport(importId);
		if (importInfo == null) return new string[0];
		if (importInfo.progressIds == null) return new string[0];
		return importInfo.progressIds.ToArray();
	}

	public int GetSuccessOrDuplicateCount(string importId)
	{ 
		ImportInfo importInfo;
		bool success = dictImports.TryGetValue(importId, out importInfo);
		if (importId.Equals("All"))
			return (success) ? importInfo.success : 0; 
		return (success) ? importInfo.success + importInfo.duplicate : 0;
	}
	public int GetSuccessCount(string importId)
	{
		ImportInfo importInfo;
		bool success = dictImports.TryGetValue(importId, out importInfo);
		return (success) ? importInfo.success : 0;
	}
	public int GetProcessedCount(string importId)
	{
		ImportInfo importInfo;
		bool success = dictImports.TryGetValue(importId, out importInfo);
		return (success) ? importInfo.processed : 0; 
	}
	public int GetDuplicateCount(string importId)
	{
		ImportInfo importInfo;
		bool success = dictImports.TryGetValue(importId, out importInfo);
		return (success) ? importInfo.duplicate : 0;
	}
	public int GetTotalCount(string importId)
	{
		ImportInfo importInfo;
		bool success = dictImports.TryGetValue(importId, out importInfo);
		return (success) ? importInfo.total : 0;
	}
	public bool GetFinished(string importId)
	{
		if (importId.Equals("")) return true;
		ImportInfo importInfo;
		bool success = dictImports.TryGetValue(importId, out importInfo);
		return (success) ? importInfo.finished : true;
	}
	public string[] GetImportIds()
	{
		return dictImports.Keys.ToArray();
	}
	public string[] GetImportIdsFromHash(string imageHash)
	{
		var temp = colHashes.FindById(imageHash);
		if (temp == null) return null;
		if (temp.imports == null) return null;
		return temp.imports.ToArray();
	}
	public string[] GetTabIds()
	{
		return dictTabs.Keys.ToArray();
	}
	public string GetName(string tabId)
	{
		return (dictTabs.ContainsKey(tabId)) ? (dictTabs[tabId].tabName == null) ? "Import" : dictTabs[tabId].tabName : "Import";
	}
	public string GetImportId(string tabId)
	{
		return (dictTabs.ContainsKey(tabId)) ? dictTabs[tabId].importId : "";
	}
	public string GetSimilarityHash(string tabId)
	{
		return (dictTabs.ContainsKey(tabId)) ? dictTabs[tabId].similarityHash : "";
	}
	public int GetTabType(string tabId)
	{
		return (dictTabs.ContainsKey(tabId)) ? dictTabs[tabId].tabType : 0;
	}
	public void CreateTab(string _tabId, int _tabType, string _tabName, int totalCount=0, string _importId="", string _groupId="", string _tag="", string _similarityHash="")
	{
		try {
			var tabInfo = new TabInfo { 
				tabId = _tabId,
				tabType = _tabType,
				tabName = _tabName,
				importId = _importId,
				groupId = _groupId,
				tag = _tag,
				similarityHash = _similarityHash,			
			};
			dictTabs[_tabId] = tabInfo;
			colTabs.Insert(tabInfo);
		} 
		catch (Exception ex) { 
			GD.Print("Database::CreateTab() : ", ex); 
			return; 
		}
	}
	public void RemoveTab(string tabId)
	{
		try {
			dictTabs.Remove(tabId);
			colTabs.Delete(tabId);
		} 
		catch (Exception ex) { 
			GD.Print("Database::RemoveTab() : ", ex); 
			return; 
		}
	}

/*==============================================================================*/
/*                                   Similarity                                 */
/*==============================================================================*/
	public float ColorSimilarity(float[] h1, float[] h2)
	{
		int numColors = h1.Length, same = 0;
		float difference = 0f;
		for (int color = 0; color < numColors; color++) {
			float percent1 = h1[color], percent2 = h2[color];
			difference += Math.Abs(percent1-percent2);
			if (percent1 > 0 && percent2 > 0) same++;
			else if (percent1 == percent2) same++;
		}
		
		float p1 = 100f * (float)same/numColors;
		float p2 = 100f-(difference/2f);

		return 0.5f * (p1+p2);
	}

	public double DifferenceSimilarity(ulong h1, ulong h2)
	{
		return CompareHash.Similarity(h1, h2);
	}
	
	public double PerceptualSimilarity(string h1, string h2)
	{
		var phash1 = new ImageMagick.PerceptualHash(h1);
		var phash2 = new ImageMagick.PerceptualHash(h2);
		return (1000.0 - phash1.SumSquaredDistance(phash2)) / 10;
	}

	public float GetAverageSimilarityTo(string compareHash, string imageHash)
	{
		var hashInfo1 = (dictHashes.ContainsKey(compareHash)) ? dictHashes[compareHash] : colHashes.FindById(compareHash);
		var hashInfo2 = (dictHashes.ContainsKey(imageHash)) ? dictHashes[imageHash] : colHashes.FindById(imageHash);
		float color = ColorSimilarity(hashInfo1.colorHash, hashInfo2.colorHash);
		double difference = DifferenceSimilarity(hashInfo1.differenceHash, hashInfo2.differenceHash);
		double perceptual = PerceptualSimilarity(hashInfo1.perceptualHash, hashInfo2.perceptualHash);
		return (color+(float)perceptual+(float)difference)/3f;
	}

	public float GetColorSimilarityTo(string compareHash, string imageHash)
	{
		var hashInfo1 = (dictHashes.ContainsKey(compareHash)) ? dictHashes[compareHash] : colHashes.FindById(compareHash);
		var hashInfo2 = (dictHashes.ContainsKey(imageHash)) ? dictHashes[imageHash] : colHashes.FindById(imageHash);
		return ColorSimilarity(hashInfo1.colorHash, hashInfo2.colorHash);
	}

	public float GetDifferenceSimilarityTo(string compareHash, string imageHash)
	{
		var hashInfo1 = (dictHashes.ContainsKey(compareHash)) ? dictHashes[compareHash] : colHashes.FindById(compareHash);
		var hashInfo2 = (dictHashes.ContainsKey(imageHash)) ? dictHashes[imageHash] : colHashes.FindById(imageHash);
		double difference = DifferenceSimilarity(hashInfo1.differenceHash, hashInfo2.differenceHash);
		return (float)difference;
	}

	public float GetPerceptualSimilarityTo(string compareHash, string imageHash)
	{
		var hashInfo1 = (dictHashes.ContainsKey(compareHash)) ? dictHashes[compareHash] : colHashes.FindById(compareHash);
		var hashInfo2 = (dictHashes.ContainsKey(imageHash)) ? dictHashes[imageHash] : colHashes.FindById(imageHash);
		double perceptual = PerceptualSimilarity(hashInfo1.perceptualHash, hashInfo2.perceptualHash);
		return (float) perceptual;
	}


}
