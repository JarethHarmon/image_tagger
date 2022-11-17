using Godot;
using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
	private int progressSectionSize = 32;
	private string metadataPath;
	public void SetMetadataPath(string path) { metadataPath = path; }

	/* may eventually reduce and merge these (especially merging dbGroups with dbTags) */
	private LiteDatabase dbHashes, dbImports, dbGroups, dbTags;
	//private LiteDatabase[] dbThumbnails = new LiteDatabas[256]; // FF=255, 00=0, 7F=128	(7*16+F=8*16=128)
	
	private ILiteCollection<HashInfo> colHashes;
	private ILiteCollection<ImportInfo> colImports;
	private ILiteCollection<ImportProgress> colProgress;
	private ILiteCollection<GroupInfo> colGroups;
	private ILiteCollection<TagInfo> colTags;
	private ILiteCollection<TabInfo> colTabs;

	/* for thread safety, it might be better to make all of these into concurrent dictionaries */
	private HashInfo currentHashInfo = new HashInfo();
	private ConcurrentDictionary<string, ImportInfo> dictImports = new ConcurrentDictionary<string, ImportInfo>();
	private Dictionary<string, GroupInfo> dictGroups = new Dictionary<string, GroupInfo>();
	private Dictionary<string, TagInfo> dictTags = new Dictionary<string, TagInfo>();
	private Dictionary<string, TabInfo> dictTabs = new Dictionary<string, TabInfo>();

	private ImageScanner scanner;
	private Importer.ImageImporter importer;
	private Node globals, signals;

/*==============================================================================*/
/*                                 Initialization                               */
/*==============================================================================*/
	public override void _Ready()
	{
		scanner = (ImageScanner) GetNode("/root/ImageScanner");
		importer = (Importer.ImageImporter) GetNode("/root/ImageImporter");
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
			//colHashes.EnsureIndex(x => x.tags.Length);
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

		if (main.tags != null && sub.tags != null) main.tags = main.tags.Union(sub.tags).ToArray();//MergeHashSets(main.tags, sub.tags);
		else if (sub.tags != null) main.tags = sub.tags; 

		if (main.differenceHash == 0) main.differenceHash = sub.differenceHash;
		if (main.colorHash == null) main.colorHash = sub.colorHash;
		if (main.perceptualHash == null) main.perceptualHash = sub.perceptualHash;
	}

	public void StoreTempHashInfo(string importId, string progressId, HashInfo hashInfo)
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
	
	public HashInfo GetHashInfo(string importId, string imageHash)
	{
		lock (tempHashInfo) { 
			if (tempHashInfo.ContainsKey(importId)) 
				if (tempHashInfo[importId].ContainsKey(imageHash))
					return tempHashInfo[importId][imageHash]; 
		}
		return colHashes.FindById(imageHash);
	}
	
/*==============================================================================*/
/*                                  Hash Database                               */
/*==============================================================================*/
	private int _lastQueriedCount = 0;
	public int GetLastQueriedCount() { return _lastQueriedCount; }
	
	public void AddRating(string imageHash, string ratingName, int ratingValue)
	{
		try {
			if (currentHashInfo.ratings == null) currentHashInfo.ratings = new Dictionary<string, int>();
			currentHashInfo.ratings[ratingName] = ratingValue;
		
			int sum = 0;
			foreach (string _name in currentHashInfo.ratings.Keys)
				sum += currentHashInfo.ratings[_name];
			
			currentHashInfo.ratingSum = sum;
			currentHashInfo.ratingAvg = (int)Math.Round((double)sum/Math.Max(1, currentHashInfo.ratings.Count));

			colHashes.Update(currentHashInfo);
		} catch (Exception ex) { GD.Print("Database::AddRating() : ", ex); return; }		
	}
	public int GetRating(string imageHash, string ratingName)
	{
		try {
			if (currentHashInfo.imageHash.Equals(imageHash))
				if (currentHashInfo.ratings != null)
					if (currentHashInfo.ratings.ContainsKey(ratingName))
						return currentHashInfo.ratings[ratingName];
			return 0;
		} catch (Exception ex) { GD.Print("Database::GetRating() : ", ex); return 0; }	
	}
	
	/*public void PopulateDictHashes(string[] imageHashes)
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
	}*/

	// returning long would be better, but godot does not marshal long into its 64bit integer type for some reason
	// instead return as string, convert with .to_int() and convert to size with String.humanize_size(int)
	public string GetFileSize(string imageHash) 
	{
		return currentHashInfo.size.ToString();
	}
	
	public string[] GetHashPaths(string imageHash)
	{
		try {
			if (currentHashInfo.imageHash.Equals(imageHash))
				return currentHashInfo.paths.ToArray();
			var result = colHashes.FindById(imageHash);
			if (result == null) return new string[0];
			return result.paths.ToArray();
		} catch (Exception ex) { return new string[0]; }
	}
	
	public string GetImageName(string imageHash)
	{
		try {
			if (currentHashInfo.imageHash.Equals(imageHash))
				return currentHashInfo.imageName;
			var result = colHashes.FindById(imageHash);
			if (result == null) return "";
			return (result.imageName == null) ? "" : result.imageName;
		} catch (Exception ex) { return ""; }
	}

	public int GetImageFormat(string imageHash)
	{
		try {
			if (currentHashInfo.imageHash.Equals(imageHash))
				return currentHashInfo.imageType;
			var result = colHashes.FindById(imageHash);
			if (result == null) return -1;
			return result.imageType;
		} catch (Exception ex) { return -1; }
	}

	public string[] GetTags(string imageHash)
	{
		try {
			if (currentHashInfo.imageHash.Equals(imageHash))
				return (currentHashInfo.tags is null) ? Array.Empty<string>() : currentHashInfo.tags;
				//return currentHashInfo.tags.ToArray();
			var result = colHashes.FindById(imageHash);
			if (result?.tags is null) return new string[0];
			return result.tags;
			//return result.tags.ToArray();
		} catch (Exception ex) { return new string[0]; }
	}
	
	public void LoadCurrentHashInfo(string imageHash)
	{
		try {
			var tmp = colHashes.FindById(imageHash);
			if (tmp != null) currentHashInfo = tmp;
		}
		catch (Exception ex) { return; }
	}

	public string GetDiffHash(string imageHash)
	{
		try {
			if (currentHashInfo.imageHash.Equals(imageHash))
				return currentHashInfo.differenceHash.ToString();
			return string.Empty;
		} catch { return string.Empty; }
	}
	public float[] GetColorHash(string imageHash) 
	{
		try {
			if (currentHashInfo.imageHash.Equals(imageHash))
				return currentHashInfo.colorHash;
			return new float[0];
		} catch { GD.Print("ERR"); return new float[0]; }
	}
	public string GetCreationTime(string imageHash)
	{
		try {
			if (currentHashInfo.imageHash.Equals(imageHash))
				return new DateTime(currentHashInfo.creationTime).ToString();
			return string.Empty;
		} catch { return string.Empty; }
	}

	public string[] QueryDatabase(string tabId, int offset, int count, string[] tagsAll, string[] tagsAny, string[] tagsNone, string[] tagsComplex, int sort=(int)Sort.SHA256, int order=(int)Order.ASCENDING, bool countResults=false, int similarity=(int)Similarity.AVERAGE)
	{
		try {
			var label = GetNode<Label>("/root/main/Label");
			var results = new List<string>();
			int tabType = GetTabType(tabId);
			if (tabType == (int)Tab.IMPORT_GROUP) {
				var now = DateTime.Now;
				string importId = GetImportId(tabId);
				var hashes = _QueryImport(importId, offset, count, tagsAll, tagsAny, tagsNone, tagsComplex, sort, order, countResults);
				label.Text = (DateTime.Now-now).ToString();
				return hashes;
			}
			// image group
			// tag
			else if (tabType == (int)Tab.SIMILARITY) {
				var now = DateTime.Now;
				string imageHash = GetSimilarityHash(tabId);
				var temp = colHashes.FindById(imageHash);
				if (temp == null) return new string[0];
				var hashes = _QueryBySimilarity("All", temp.colorHash, temp.differenceHash, temp.perceptualHash, offset, count, tagsAll, tagsAny, tagsNone, similarity); 
				label.Text = (DateTime.Now-now).ToString();
				return hashes;
			}
			return Array.Empty<string>();
		} catch (Exception ex) { GD.Print("Database::QueryDatabase() : ", ex); return new string[0]; }
	}

	enum Types { ALL, ANY, NONE }
		
	private BsonExpression CreateCondition(string[] tags, int numTags, int type)
	{
		// NONE
		if (type == (int)Types.NONE)
			return (BsonExpression)string.Format("($.tags[*] ANY IN {0})!=true", BsonMapper.Global.Serialize(tags));

		// one ALL or ANY
		if (numTags == 1)
			return Query.Contains("$.tags[*] ANY", tags[0]);
		
		// multiple ALL or ANY 
		var list = new List<BsonExpression>();
		foreach (string tag in tags)
			list.Add(Query.Contains("$.tags[*] ANY", tag));

		// check AND or OR
		if (type == (int)Types.ALL)
			return Query.And(list.ToArray());
		return Query.Or(list.ToArray());
	}

	//private List<HashInfo> _QueryImport(string importId, int offset, int count, string[] tagsAll, string[] tagsAny, string[] tagsNone, string[] tagsComplex, int sort=(int)Sort.SHA256, int order=(int)Order.ASCENDING, bool countResults=false)
	private string[] _QueryImport(string importId, int offset, int count, string[] tagsAll, string[] tagsAny, string[] tagsNone, string[] tagsComplex, int sort=(int)Sort.SHA256, int order=(int)Order.ASCENDING, bool countResults=false)
	{
		try {
			bool counted=false;

			if (tagsAll.Length == 0 && tagsAny.Length == 0 && tagsNone.Length == 0 && tagsComplex.Length == 0) {
				_lastQueriedCount = (importId.Equals("All")) ? GetSuccessCount(importId) : GetSuccessOrDuplicateCount(importId);
				counted = true;
			}

			var rng = new Random(); // for SortBy.Random (if I can figure out how to do so)
			var query = colHashes.Query();

			if (importId != "All") query = query.Where(x => x.imports.Contains(importId));
			//if (groupId != "") query = query.Where(x => x.groups.Contains(groupId));
			
			// this will eventually be handled automatically on the gdscript side (once the ui is replaced to something easier to take advantage of)
			// there is also a lot of optimization to be done here
			var tagArrays = new List<Dictionary<string, HashSet<string>>>();
			var newAll = new HashSet<string>(tagsAll);
			var newAny = new HashSet<string>(tagsAny);
			var newNone = new HashSet<string>(tagsNone);

			if (tagsAll.Length > 0 || tagsAny.Length > 0 || tagsNone.Length > 0) {
				string condition = "";
				if (tagsAll.Length > 0) foreach (string tag in tagsAll) condition += tag + ",";
				condition += "%";
				if (tagsAny.Length > 0) foreach (string tag in tagsAny) condition += tag + ",";
				condition += "%";
				if (tagsNone.Length > 0) foreach (string tag in tagsNone) condition += tag + ",";
				var tempList = new List<string>(tagsComplex);
				tempList.Add(condition);
				tagsComplex = tempList.ToArray();
			}

			if (tagsComplex.Length > 0) {
				var tempAll = new HashSet<string>();
				var tempAny = new HashSet<string>();
				var tempNone = new HashSet<string>();

				foreach (string condition in tagsComplex) {
					string[] sections = condition.Split(new string[1]{"%"}, StringSplitOptions.None);

					if (sections.Length < 3) { GD.Print("ERROR"); continue; }
					string[] _all = sections[0].Split(new string[1]{","}, StringSplitOptions.RemoveEmptyEntries);
					string[] _any = sections[1].Split(new string[1]{","}, StringSplitOptions.RemoveEmptyEntries);
					string[] _none = sections[2].Split(new string[1]{","}, StringSplitOptions.RemoveEmptyEntries);

					foreach (string tag in _all) tempAll.Add(tag);
					foreach (string tag in _any) tempAny.Add(tag);
					foreach (string tag in _none) tempNone.Add(tag);

					var dict = new Dictionary<string, HashSet<string>>();
					dict["All"] = new HashSet<string>(_all);
					dict["Any"] = new HashSet<string>(_any);
					dict["None"] = new HashSet<string>(_none);
					tagArrays.Add(dict);
				}

				foreach (string tag in tempAll) {
					bool allContain = true;
					foreach (Dictionary<string, HashSet<string>> condition in tagArrays) {
						if (!condition["All"].Contains(tag)) { newAny.Add(tag); allContain = false; }
						if (!allContain) break;
					}
					if (allContain) newAll.Add(tag);
				}
				foreach (Dictionary<string, HashSet<string>> condition in tagArrays)
					foreach (string tag in condition["Any"])
						newAny.Add(tag);
				foreach (string tag in tempNone) {
					bool allContain = true;
					foreach (Dictionary<string, HashSet<string>> condition in tagArrays) {
						if (!condition["None"].Contains(tag)) allContain = false;
						if (!allContain) break;
					}
					if (allContain) newNone.Add(tag);
				}
			}

			tagsAll = newAll.ToArray();
			tagsAny = newAny.ToArray();
			tagsNone = newNone.ToArray();

			if (tagsAll.Length > 0) foreach (string tag in tagsAll) query = query.Where(x => x.tags.Contains(tag));
			if (tagsAny.Length > 0) query = query.Where("$.tags ANY IN @0", BsonMapper.Global.Serialize(tagsAny));
			//if (tagsNone.Length > 0) foreach (string tag in tagsNone) query = query.Where(x => !x.tags.Contains(tag));
			if (tagsNone.Length > 0) query = query.Where("($.tags[*] ANY IN @0)!=true", BsonMapper.Global.Serialize(tagsNone));

			if (tagsComplex.Length > 0 && tagsAny.Length > 0) {
				var condList = new List<BsonExpression>();
				foreach (Dictionary<string, HashSet<string>> condition in tagArrays) {
					if (condition["All"].Count == 0 && condition["Any"].Count == 0 && condition["None"].Count == 0) continue;

					var _list = new List<BsonExpression>();
					if (condition["All"].Count > 0)
						_list.Add(CreateCondition(condition["All"].ToArray(), condition["All"].Count, (int)Types.ALL));
					if (condition["Any"].Count > 0)
						_list.Add(CreateCondition(condition["Any"].ToArray(), condition["Any"].Count, (int)Types.ANY));
					if (condition["None"].Count > 0)
						_list.Add(CreateCondition(condition["None"].ToArray(), condition["None"].Count, (int)Types.NONE));

					if (_list.Count == 0) continue;
					else if (_list.Count == 1) condList.Add(_list[0]);
					else condList.Add(Query.And(_list.ToArray()));
				}
				if (condList.Count == 1)
					query = query.Where(condList[0]);
				else if (condList.Count > 1)
					query = query.Where(Query.Or(condList.ToArray()));
			}

			if (countResults && !counted) _lastQueriedCount = query.Count(); // slow

			if (sort == (int)Sort.SIZE) query = (order == (int)Order.ASCENDING) ? query.OrderBy(x => x.size) : query.OrderByDescending(x => x.size);
			else if (sort == (int)Sort.PATH) query = (order == (int)Order.ASCENDING) ? query.OrderBy(x => x.paths.FirstOrDefault()) : query.OrderByDescending(x => x.paths.FirstOrDefault());
			else if (sort == (int)Sort.NAME) query = (order == (int)Order.ASCENDING) ? query.OrderBy(x => x.imageName) : query.OrderByDescending(x => x.imageName);
			else if (sort == (int)Sort.CREATION_TIME) query = (order == (int)Order.ASCENDING) ? query.OrderBy(x => x.creationTime) : query.OrderByDescending(x => x.creationTime);
			else if (sort == (int)Sort.UPLOAD_TIME) query = (order == (int)Order.ASCENDING) ? query.OrderBy(x => x.uploadTime) : query.OrderByDescending(x => x.uploadTime);
			else if (sort == (int)Sort.EDIT_TIME) query = (order == (int)Order.ASCENDING) ? query.OrderBy(x => x.lastWriteTime) : query.OrderByDescending(x => x.lastWriteTime);
			else if (sort == (int)Sort.DIMENSIONS) query = (order == (int)Order.ASCENDING) ? query.OrderBy(x => x.width*x.height) : query.OrderByDescending(x => x.width*x.height);
			else if (sort == (int)Sort.TAG_COUNT) query = (order == (int)Order.ASCENDING) ? query.OrderBy(x => x.tags.Length) : query.OrderByDescending(x => x.tags.Length);
			else if (sort == (int)Sort.IMAGE_COLOR) query = (order == (int)Order.ASCENDING) ? query.OrderBy(x => x.colorHash[0] + x.colorHash[15] + x.colorHash[7]) : query.OrderByDescending(x => x.colorHash[0] + x.colorHash[15] + x.colorHash[7]);
			//else if (sort == (int)Sort.IMAGE_COLOR) query = (order == (int)Order.ASCENDING) ? query.OrderBy(x => x.numColors) :  query.OrderByDescending(x => x.numColors);
			else if (sort == (int)Sort.RANDOM) {
				// other (faster) method throws "System.Exception: LiteDB ENSURE: page type must be index page"
				query = query.OrderBy("RANDOM()");
				return query.ToEnumerable().Select(x => x.imageHash).Skip(offset).Take(count).ToArray();
			}
			// need a way to handle this that allows custom user ratings
			else if (sort == (int)Sort.RATING_QUALITY) query = (order == (int)Order.ASCENDING) ? query.OrderBy(x => x.ratings["Quality"]) : query.OrderByDescending(x => x.ratings["Quality"]);
			else if (sort == (int)Sort.RATING_APPEAL) query = (order == (int)Order.ASCENDING) ? query.OrderBy(x => x.ratings["Appeal"]) : query.OrderByDescending(x => x.ratings["Appeal"]);
			else if (sort == (int)Sort.RATING_ART_STYLE) query = (order == (int)Order.ASCENDING) ? query.OrderBy(x => x.ratings["Art"]) : query.OrderByDescending(x => x.ratings["Art"]);
			else if (sort == (int)Sort.RATING_SUM) query = (order == (int)Order.ASCENDING) ? query.OrderBy(x => x.ratings["Sum"]) : query.OrderByDescending(x => x.ratings["Sum"]);
			else if (sort == (int)Sort.RATING_AVERAGE) query = (order == (int)Order.ASCENDING) ? query.OrderBy(x => x.ratings["Average"]) : query.OrderByDescending(x => x.ratings["Average"]);			
			else query = (order == (int)Order.ASCENDING) ? query.OrderBy(x => x.imageHash) : query.OrderByDescending(x => x.imageHash);
			
			//GD.Print(query.GetPlan());

			/*var result = query.Select(x => x.imageHash);

			var now = DateTime.Now;
			int count2 = result.Count();
			GD.Print($"result.Count[{count2}]: ", DateTime.Now-now);

			now = DateTime.Now;
			int count1 = query.Count();
			GD.Print($"query.Count[{count1}]: ", DateTime.Now-now);*/

			//GD.Print(unchecked((ulong)-6458626015914340928));
			
			return query.Select(x => x.imageHash).Offset(offset).Limit(count).ToArray();
		} 
		catch (Exception ex) { 
			GD.Print("Database::_QueryDatabase() : ", ex); 
			return Array.Empty<string>();
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

	internal static (double[], double[]) GetChannelPerceptualHash(string perceptualHash)
	{
		double[] srgbHash = new double[7], hclpHash = new double[7];
		for (int i = 0; i < 14; i++)
		{
			// replace with ReadOnlySpan<char> for .net6+
			if (!int.TryParse(perceptualHash.Substring(i * 5, 5), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hex))
				return (Array.Empty<double>(), Array.Empty<double>());

			var value = (ushort)hex / Math.Pow(10.0, hex >> 17);
			if ((hex & (1 << 16)) != 0)
				value = -value;
			if (i < 7)
				srgbHash[i] = value;
			else
				hclpHash[i - 7] = value;
		}
		return (srgbHash, hclpHash);
	}

	public string[] _QueryBySimilarity(string importId, float[] colorHash, ulong differenceHash, string perceptualHash, int offset, int count, string[] tagsAll, string[] tagsAny, string[] tagsNone, int similarityMode=(int)Similarity.AVERAGE)
	{
		var query = colHashes.Query();

		bool counted=false;
		if (tagsAll.Length == 0 && tagsAny.Length == 0 && tagsNone.Length == 0) {
			_lastQueriedCount = (importId.Equals("All")) ? GetSuccessCount(importId) : GetSuccessOrDuplicateCount(importId);
			counted = true;
		}

		if (importId != "All") query = query.Where(x => x.imports.Contains(importId));
		if (tagsAll.Length > 0) foreach (string tag in tagsAll) query = query.Where(x => x.tags.Contains(tag));
		if (tagsAny.Length > 0) query = query.Where("$.tags ANY IN @0", BsonMapper.Global.Serialize(tagsAny));
		if (tagsNone.Length > 0) foreach (string tag in tagsNone) query = query.Where(x => !x.tags.Contains(tag));

		if (similarityMode == (int)Similarity.AVERAGE) {
			(double[] srgb1, double[] hclp1) = GetChannelPerceptualHash(perceptualHash.Substring(0, 70));
			(double[] srgb2, double[] hclp2) = GetChannelPerceptualHash(perceptualHash.Substring(70, 70));
			(double[] srgb3, double[] hclp3) = GetChannelPerceptualHash(perceptualHash.Substring(140, 70));

			BsonValue arr1 = BsonMapper.Global.Serialize(srgb1), arr2 = BsonMapper.Global.Serialize(hclp1),
				arr3 = BsonMapper.Global.Serialize(srgb2), arr4 = BsonMapper.Global.Serialize(hclp2),
				arr5 = BsonMapper.Global.Serialize(srgb3), arr6 = BsonMapper.Global.Serialize(hclp3),
				arrColor = BsonMapper.Global.Serialize(colorHash);

			string difference = $"SIMILARITY_COENM($.differenceHash, {(BsonValue)(long)differenceHash})";
			string color = $"SIMILARITY_COLOR($.colorHash, {arrColor})";
			string perceptual = $"SIMILARITY_MAGICK_PERCEPTUAL({arr1}, {arr3}, {arr5}, {arr2}, {arr4}, {arr6}, $.perceptualHash)";

			query = query.OrderByDescending($"{difference} + {color} + {perceptual}");
		}
		else if (similarityMode == (int)Similarity.COLOR) {
			BsonValue arrColor = BsonMapper.Global.Serialize(colorHash);
			string color = $"SIMILARITY_COLOR($.colorHash, {arrColor})";
			query = query.OrderByDescending(color);
		}			
		else if (similarityMode == (int)Similarity.PERCEPTUAL) {
			(double[] srgb1, double[] hclp1) = GetChannelPerceptualHash(perceptualHash.Substring(0, 70));
			(double[] srgb2, double[] hclp2) = GetChannelPerceptualHash(perceptualHash.Substring(70, 70));
			(double[] srgb3, double[] hclp3) = GetChannelPerceptualHash(perceptualHash.Substring(140, 70));

			BsonValue arr1 = BsonMapper.Global.Serialize(srgb1), arr2 = BsonMapper.Global.Serialize(hclp1),
				arr3 = BsonMapper.Global.Serialize(srgb2), arr4 = BsonMapper.Global.Serialize(hclp2),
				arr5 = BsonMapper.Global.Serialize(srgb3), arr6 = BsonMapper.Global.Serialize(hclp3);
			
			string perceptual = $"SIMILARITY_MAGICK_PERCEPTUAL({arr1}, {arr3}, {arr5}, {arr2}, {arr4}, {arr6}, $.perceptualHash)";
			query = query.OrderByDescending(perceptual);
		}			
		else {
			string difference = $"SIMILARITY_COENM($.differenceHash, {(BsonValue)(long)differenceHash})";
			query = query.OrderByDescending(difference);
		}

		if (!counted) _lastQueriedCount = query.Count();

		return query.Select(x => x.imageHash).Offset(offset).Limit(count).ToArray();	
	}

	public void BulkAddTags(string[] imageHashes, string[] tags)
	{
		try {
			var hs = new HashSet<string>(imageHashes);
			var query = colHashes.Query();
			query = query.Where(x => hs.Contains(x.imageHash));
			var list = query.ToList();

			if (hs.Contains(currentHashInfo.imageHash)) {
				if (currentHashInfo.tags == null) 
					currentHashInfo.tags = tags;
				else 
					currentHashInfo.tags = currentHashInfo.tags.Union(tags).ToArray();
			}

			foreach (HashInfo info in list) {
				if (info.tags == null) info.tags = tags;
				else info.tags = info.tags.Union(tags).ToArray();
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
			var hs = new HashSet<string>(imageHashes);
			var query = colHashes.Query();
			query = query.Where(x => hs.Contains(x.imageHash));
			var list = query.ToList();

			if (hs.Contains(currentHashInfo.imageHash))
				if (currentHashInfo.tags != null) 
					if (currentHashInfo.tags.Length > 0)
						currentHashInfo.tags = currentHashInfo.tags.Except(tags).ToArray();

			foreach (HashInfo info in list) {
				if (info == null) continue;
				if (info.tags == null) continue;
				if (info.tags.Length == 0) continue;
				info.tags = info.tags.Except(tags).ToArray();
				if (info.tags.Length == 0) info.tags = null;
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

			dictImports[_id] = importInfo;
			colImports.Insert(importInfo);
			colProgress.Insert(listProgress);
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
			dictImports[importId] = importInfo;
			lock(tempHashInfo) tempHashInfo.Remove(importId);
			string[] tabs = GetTabIDs(importId);
			signals.Call("emit_signal", "finish_import_buttons", tabs);
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

			if (importInfo.progressIds.Count == 0) FinishImport(importId);
		}

		foreach (HashInfo hashInfo in hashInfoList) {
			if (hashInfo.imports == null) hashInfo.imports = new HashSet<string>();
			hashInfo.imports.Add(importId);
		}
		colHashes.Upsert(hashInfoList);
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

	public void UpdateImportCount(string importId, int result)
	{
		try {
			var importInfo = GetImport(importId);
			var allInfo = GetImport("All");

			if (result == (int)ImportCode.SUCCESS) {
				allInfo.success++;
				importInfo.success++;
			}
			else if (result == (int)ImportCode.DUPLICATE) importInfo.duplicate++;
			else if (result == (int)ImportCode.IGNORED) importInfo.ignored++;
			else importInfo.failed++;
			importInfo.processed++;

			AddImport("All", allInfo);
			AddImport(importId, importInfo);
		}
		catch(Exception ex) { 
			GD.Print("Database::UpdateImportCount() : ", ex); 
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
		double distance = phash1.SumSquaredDistance(phash2);
		return (100.0 - Math.Sqrt(distance)); // seems to range from 0-10000 (so 0 would be 100% similar, 10000 would be 0% similar; 100 would be 68.4%)
	}

	public float GetAverageSimilarityTo(string compareHash, string imageHash)
	{
		var hashInfo1 = colHashes.FindById(compareHash);
		var hashInfo2 = colHashes.FindById(imageHash);
		float color = ColorSimilarity(hashInfo1.colorHash, hashInfo2.colorHash);
		double difference = DifferenceSimilarity(hashInfo1.differenceHash, hashInfo2.differenceHash);
		double perceptual = PerceptualSimilarity(hashInfo1.perceptualHash, hashInfo2.perceptualHash);
		return (color+(float)perceptual+(float)difference)/3f;
	}

	public float GetColorSimilarityTo(string compareHash, string imageHash)
	{
		var hashInfo1 = colHashes.FindById(compareHash);
		var hashInfo2 = colHashes.FindById(imageHash);
		return ColorSimilarity(hashInfo1.colorHash, hashInfo2.colorHash);
	}

	public float GetDifferenceSimilarityTo(string compareHash, string imageHash)
	{
		var hashInfo1 = colHashes.FindById(compareHash);
		var hashInfo2 = colHashes.FindById(imageHash);
		double difference = DifferenceSimilarity(hashInfo1.differenceHash, hashInfo2.differenceHash);
		return (float)difference;
	}

	public float GetPerceptualSimilarityTo(string compareHash, string imageHash)
	{
		var hashInfo1 = colHashes.FindById(compareHash);
		var hashInfo2 = colHashes.FindById(imageHash);
		double perceptual = PerceptualSimilarity(hashInfo1.perceptualHash, hashInfo2.perceptualHash);
		return (float) perceptual;
	}


}
