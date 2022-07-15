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

// query by similarity should open a new tab specifically for that purpose (similarity tab, can then further limit it to first X or simi > n%) (can also filter by tags and change order (but not sort))
// this way I also do not need to include diffHash and colorHash inside groups/imports


// I think I will remove lists of hashes from every class, now should query directly over hashInfo to find things with specific groupIds/importIds/tagIds/etc
/*=========================================================================================
										Classes
=========================================================================================*/
	public class SortBy
	{
		public const int FileHash = 0;
		public const int FilePath = 1;
		public const int FileSize = 2;
		public const int FileCreationUtc = 3;
		public const int FileUploadUtc = 4;
		public const int TagCount = 5;
		public const int Random = 6;
	}
	public class OrderBy
	{
		public const int Ascending = 0;
		public const int Descending = 1;
	}

	// global full_image (id copy/move full images) and thumbnail storage paths should be auto-blacklisted
	public class HashInfo
	{
		public string imageHash { get; set; }			// the komi64 hash of the image (may use SHA512/256 instead)
		public string gobPath { get; set; }				// the path the file uses if it is copied/moved by the program to a central location
		
		public ulong diffHash { get; set; }				// the CoenM.ImageHash::DifferenceHash() of the thumbnail
		public float[] colorHash { get; set; }			// the ColorHash() of the thumbnail
		
		public int flags { get; set; }					// a FLAG integer used for toggling filter, etc
		public int thumbnailType { get; set; }			// jpg/png
		public int type { get; set; }					// see ImageType
		public long size { get; set; }					// the length of the file in bytes
		public long creationUtc { get; set; }			// the time the file was created in ticks
		public long uploadUtc { get; set; }				// the time the file was uploaded to the database in ticks
		
		public HashSet<string> imports { get; set; }	// the importIds of the imports the image was a part of
		public HashSet<string> groups { get; set; }		// the groupIds of the groups the image is a part of
		public HashSet<string> paths { get; set; }		// every path the image has been found at on the user's computer
		public HashSet<string> tags { get; set; }		// every tag that has been applied to the image
		public Dictionary<string, int> ratings { get; set; }	// rating_name : rating/10
	}

	/* holds metadata for an import */
	public class ImportInfo
	{
		public string importId { get; set; }			// the ID of this import
		public int successCount { get; set; }			// the number of successfully imported image in this import
		public int ignoredCount { get; set; }			// the number of images that were scanned but got skipped because of the user's import settings
		public int failedCount { get; set; }			// the number of images that were scanned but failed to import (corrupt/etc)
		public int duplicateCount { get; set; }			// the number of images that were scanned but were already present in the database (basically auto-ignored)
		public int canceledCount { get; set; }			// number of paths that were removed from files when this import was canceled
		public int removedCount { get; set; }			// the number of paths that have been manually removed from this import by the user (may not keep)
		public int hiddenCount { get; set; }			// the number of paths that have been hidden from normal view for this import (for easier viewing of non-tagged images)
		public int totalCount { get; set; }				// the total original number of images included in this import (regardless of whether they successfully imported)
		public bool finished { get; set; }
		public string importName { get; set; }
		public long importTime { get; set; }
		public string[] importedHashes { get; set; }
		public string[] inProgressPaths { get; set; }
		public long[] inProgressTimes { get; set; }
		public long[] inProgressSizes { get; set; }
	}

	public class GroupInfo
	{
		public string groupId { get; set; }				// the ID of this group
		public int count { get; set; }					// the number of images in this group
		public HashSet<string> tags { get; set; }		// the tags applied to this group as a whole (may move this to hashinfo, -uses more space +easier to use  (not sure what the exact use case will be right now))
		public Dictionary<string, int> subGroups { get; set; }	// the groupIds and their listing order for subgroups of this group (for example: chapters of a manga)
	}

	public class TagInfo
	{
		public string tagId { get; set; }				// the internal ID of this tag (will probably be a simplified and standardized version of tagName)
		public string tagName { get; set; }				// the displayed name for this tag
		public HashSet<string> tagParents { get; set; }	// tags that should be auto-applied if this tag is applied
	}

public class Database : Node
{

/*=========================================================================================
									   Subclasses
=========================================================================================*/
	public class ImageType
	{
		// primarily for types with better built-in support, any random types and those the user adds will be assigend 'other'
		public const int JPG = 0;
		public const int PNG = 1;
		public const int APNG = 2;
		// ...
		public const int OTHER = 7;
		public const int FAIL = -1;
	}

/*=========================================================================================
									   Variables
=========================================================================================*/
	public bool useJournal = true;
	public string metadataPath;
	public void SetMetadataPath(string path) { metadataPath = path; }
	
	public LiteDatabase dbHashes, dbImports, dbGroups, dbTags;
	public ILiteCollection<HashInfo> colHashes;
	public ILiteCollection<ImportInfo> colImports;
	public ILiteCollection<GroupInfo> colGroups;
	public ILiteCollection<TagInfo> colTags;
	
	public Dictionary<string, HashInfo> dictHashes = new Dictionary<string, HashInfo>();		// maybe keep an array of imageHashes for those that have changed (so can iterate them and call col.Update())
	//public Dictionary<string, ImportInfo> dictImports = new Dictionary<string, ImportInfo>();
	public ConcurrentDictionary<string, ImportInfo> dictImports = new ConcurrentDictionary<string, ImportInfo>();
	public Dictionary<string, GroupInfo> dictGroups = new Dictionary<string, GroupInfo>();
	public Dictionary<string, TagInfo> dictTags = new Dictionary<string, TagInfo>();
	
	public ImageScanner iscan;
	public ImageImporter importer;
	
/*=========================================================================================
									 Initialization
=========================================================================================*/
	public override void _Ready() 
	{
		iscan = (ImageScanner) GetNode("/root/ImageScanner");
		importer = (ImageImporter) GetNode("/root/ImageImporter");
	}
	
	public int Create() 
	{
		try {
			if (useJournal) {
				dbHashes = new LiteDatabase(metadataPath + "hash_info.db");
				BsonMapper.Global.Entity<HashInfo>().Id(x => x.imageHash);
				colHashes = dbHashes.GetCollection<HashInfo>("hashes");
				colHashes.EnsureIndex("tags_index", "$.tags[*]", false);
				
				dbImports = new LiteDatabase(metadataPath + "import_info.db");
				BsonMapper.Global.Entity<ImportInfo>().Id(x => x.importId);
				colImports = dbImports.GetCollection<ImportInfo>("imports");
				
				dbGroups = new LiteDatabase(metadataPath + "group_info.db");
				BsonMapper.Global.Entity<GroupInfo>().Id(x => x.groupId);
				colGroups = dbGroups.GetCollection<GroupInfo>("groups");
				
				dbTags = new LiteDatabase(metadataPath + "tag_info.db");
				BsonMapper.Global.Entity<TagInfo>().Id(x => x.tagId);
				colTags = dbTags.GetCollection<TagInfo>("tags");				
			} 
			return 0;
		} 
		catch (Exception ex) { GD.Print("Database::Create() : ", ex); return 1; }
	}
	
	public void LoadInProgressPaths()
	{
		foreach (string iid in dictImports.Keys) {
			ImportInfo iinfo = dictImports[iid];
			if (!iinfo.finished && iinfo.inProgressPaths != null) {
				var list = new List<(string,long,long)>();
				for (int i = 0; i < iinfo.inProgressPaths.Length; i++)
					list.Add((iinfo.inProgressPaths[i], iinfo.inProgressTimes[i], iinfo.inProgressSizes[i]));
				if (list.Count > 0) {
					iscan.InsertPaths(iinfo.importId, list);
					if (iinfo.importedHashes != null)
						if (iinfo.importedHashes.Length != 0)
							importer.AddToImportedHashes(iinfo.importId, iinfo.importedHashes);
				}
				iinfo.inProgressPaths = null;
				iinfo.inProgressTimes = null;
				iinfo.inProgressSizes = null;
				iinfo.importedHashes = null;
				colImports.Update(iinfo);
			}
		}
	}
	
	public void SaveInProgressPaths()
	{
		foreach (string iid in dictImports.Keys) {
			ImportInfo iinfo = dictImports[iid];
			if (!iinfo.finished) {
				var fileArray = iscan.GetInProgressPaths(iinfo.importId);
				var paths = new List<string>();
				var times = new List<long>();
				var sizes = new List<long>();
				foreach ((string,long,long) file in fileArray) {
					paths.Add(file.Item1);
					times.Add(file.Item2);
					sizes.Add(file.Item3);
				}
				iinfo.inProgressPaths = paths.ToArray();
				iinfo.inProgressTimes = times.ToArray();
				iinfo.inProgressSizes = sizes.ToArray();
				iinfo.importedHashes = importer.GetImportedHashes(iinfo.importId);
				colImports.Update(iinfo);
			}
		}
		colImports.Update(ImportsTryGetValue("All"));			
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
	
/*=========================================================================================
									Database Access
=========================================================================================*/
	private int lastQueriedCount = 0;
	public int GetLastQueriedCount() { return lastQueriedCount; }
	
	public int GetSuccessOrDuplicateCount(string importId)
	{
		return GetImportSuccessCount(importId) + GetDuplicateCount(importId);
	}
	
	public int GetImportSuccessCount(string importId)
	{
		ImportInfo importInfo;
		dictImports.TryGetValue(importId, out importInfo);
		return importInfo.successCount;
	}
	
	public int GetDuplicateCount(string importId)
	{
		ImportInfo importInfo;
		dictImports.TryGetValue(importId, out importInfo);
		return importInfo.duplicateCount;
	}
	
	public int GetTotalCount(string importId)
	{
		ImportInfo importInfo;
		if (!dictImports.TryGetValue(importId, out importInfo)) return 0;
		return importInfo.totalCount;
	}
	public bool GetFinished(string importId)
	{
		ImportInfo importInfo;
		dictImports.TryGetValue(importId, out importInfo);
		return importInfo.finished;
	}
	public string GetImportName(string importId)
	{
		ImportInfo importInfo;
		dictImports.TryGetValue(importId, out importInfo);
		return importInfo.importName;
	}
	
	public bool DuplicateImportId(string imageHash, string importId)
	{
		if (dictHashes.ContainsKey(imageHash)) 
		{
		//	GD.Print("in dict");
			return dictHashes[imageHash].imports.Contains(importId);
		} try {
			//GD.Print("check db");
			var result = colHashes.FindById(imageHash);
			if (result == null) return false;
			return result.imports.Contains(importId);
		} catch (Exception ex) { return false; }
	}
	
	public void CreateAllInfo()
	{
		try {
			var importInfo = colImports.FindById("All");
			if (importInfo == null) {
				importInfo = new ImportInfo {
					importId = "All",
					successCount = 0,
					ignoredCount = 0,
					failedCount = 0,
					duplicateCount = 0,
					finished = true,
					importName = "All",
					importTime = 0,
				};
				colImports.Insert(importInfo);
			}
			ImportsTryAdd("All", importInfo);
			//dictImports["All"] = importInfo;
		} catch(Exception ex) { GD.Print("Database::CreateAllInfo() : ", ex); return; }
	}
	public void LoadAllImportInfo()
	{
		try {
			var results = colImports.FindAll();
			foreach (ImportInfo importInfo in results) {
				ImportsTryAdd(importInfo.importId, importInfo);
				//dictImports[importInfo.importId] = importInfo;
			}
		} catch(Exception ex) { GD.Print("Database::LoadAllImportInfo()() : ", ex); return; }
	}
	
	public void ImportsTryAdd(string importId, ImportInfo importInfo)
	{
		bool result = dictImports.TryAdd(importId, importInfo);
		if (!result) {
			ImportInfo temp;
			result = dictImports.TryGetValue(importId, out temp);
			result = dictImports.TryUpdate(importId, importInfo, temp);
		}
	}
	
	public ImportInfo ImportsTryGetValue(string importId)
	{
		ImportInfo importInfo = null;
		dictImports.TryGetValue(importId, out importInfo);
		return importInfo;
	}
	
	public void CreateImportInfo(string _importId, int _totalCount)
	{
		try {
			var importInfo = new ImportInfo {
				importId = _importId,
				successCount = 0,
				ignoredCount = 0,
				failedCount = 0,
				duplicateCount = 0,
				totalCount = _totalCount,
				finished = false,
				importName = "Import",
				importTime = DateTime.Now.Ticks,
			};
			ImportsTryAdd(_importId, importInfo);
			//dictImports[_importId] = importInfo;
			colImports.Insert(importInfo);
		} catch(Exception ex) { GD.Print("Database::CreateImportInfo() : ", ex); return; }
	}
	public void UpdateImportCount(string importId, int countResult)
	{
		try {
			// move ImportCodes to Database
			
			var importInfo = ImportsTryGetValue(importId);
			//if (importInfo == null) importInfo = colImports.FindById(importId);
			var allInfo = ImportsTryGetValue("All");
		//	if (allInfo == null) allInfo = colImports.FindById("All");
			
			if (countResult == 0) { 
				importInfo.successCount++;
				allInfo.successCount++;
			} else if (countResult == 1) {
				importInfo.duplicateCount++;
				allInfo.duplicateCount++;
			} else if (countResult == 2) {
				importInfo.ignoredCount++;
				allInfo.ignoredCount++;
			} else {
				importInfo.failedCount++;
				allInfo.failedCount++;
			}
			ImportsTryAdd("All", allInfo);
			ImportsTryAdd(importId, importInfo);
			//dictImports["All"] = allInfo;
			//dictImports[importId] = importInfo;
			//colImports.Update(importInfo);
			//colImports.Update(allInfo);			
		} catch(Exception ex) { GD.Print("Database::UpdateImportCount() : ", ex); return; }
	}
	public void FinishImport(string importId)
	{
		try {
			var importInfo = ImportsTryGetValue(importId);//colImports.FindById(importId);
			var allInfo = ImportsTryGetValue("All");//colImports.FindById("All");
			importInfo.finished = true;
			ImportsTryAdd(importId, importInfo);
			//dictImports[importId] = importInfo;
			colImports.Update(importInfo);
			colImports.Update(allInfo);
		} catch(Exception ex) { GD.Print("Database::FinishImport() : ", ex); return; }
	}
	public string[] GetAllImportIds()
	{
		return dictImports.Keys.ToArray();
	}
	
	public int AddImportId(string imageHash, string importId)
	{
		try {
			var hashInfo = colHashes.FindById(imageHash);
			if (hashInfo == null) return 1;
			hashInfo.imports.Add(importId);
			if (dictHashes.ContainsKey(imageHash))
				dictHashes[imageHash] = hashInfo;
			colHashes.Update(hashInfo);
			return 0;
		} catch (Exception ex) { return 1; }
	}
	
	// 0 = new, 1 = no change, 2 = update, -1 = fail
	public int InsertHashInfo(string _imageHash, ulong _diffHash, float[] _colorHash, int _flags, int _thumbnailType, int imageType, long imageSize, long imageCreationUtc, string importId, string imagePath)
	{
		try {
			var hashInfo = colHashes.FindById(_imageHash);
			if (hashInfo == null) {
				hashInfo = new HashInfo {
					imageHash = _imageHash,
					diffHash = _diffHash,
					colorHash = _colorHash,
					flags = _flags,
					thumbnailType = _thumbnailType,
					type = imageType,
					size = imageSize,
					creationUtc = imageCreationUtc,
					uploadUtc = DateTime.Now.Ticks,
					imports = new HashSet<string>{importId},
					paths = new HashSet<string>{imagePath},
				}; 
				colHashes.Insert(hashInfo);
				return 0;
			} else {
				bool update = false;
				if (!hashInfo.paths.Contains(imagePath)) {
					hashInfo.paths.Add(imagePath);
					update = true;
				}
				if (!hashInfo.imports.Contains(importId)) {
					hashInfo.imports.Add(importId);
					update = true;
				}
				if (update) { 
					colHashes.Update(hashInfo);
					return 2;
				} 
				return 1;
			}
		} catch (Exception ex) { GD.Print("Database::InsertHashInfo() : ", ex); return -1; }
	}
	
	public bool HashDatabaseContains(string imageHash)
	{
		try {
			// consider checking dictionary instead 
			var result = colHashes.FindById(imageHash);
			if (result == null) return false;
			return true;
		} catch (Exception ex) { return false; }
	}
	
	public string[] QueryDatabase(string importId, int offset, int count, string[] tagsAll, string[] tagsAny, string[] tagsNone, int sortBy = SortBy.FileHash, int orderBy = OrderBy.Ascending, bool countResults = false, string groupId = "")
	{
		try {
			dictHashes.Clear();
			var results = new List<string>();
			var hashInfos = _QueryDatabase(importId, offset, count, tagsAll, tagsAny, tagsNone, sortBy, orderBy, countResults, groupId);
			if (hashInfos == null) return new string[0];
			foreach(HashInfo hashInfo in hashInfos) {
				results.Add(hashInfo.imageHash);
				dictHashes[hashInfo.imageHash] = hashInfo;
			}

			return results.ToArray();
		} catch (Exception ex) { GD.Print("Database::QueryDatabase() : ", ex); return new string[0]; }
	}
	private List<HashInfo> _QueryDatabase(string importId, int offset, int count, string[] tagsAll, string[] tagsAny, string[] tagsNone, int sortBy = SortBy.FileHash, int orderBy = OrderBy.Ascending, bool countResults = false, string groupId = "")
	{
		try {
			bool sortByTagCount = false, sortByRandom = false, counted=false;
			string columnName = "_Id";

			if (sortBy == SortBy.FileSize) columnName = "size";
			else if (sortBy == SortBy.FileCreationUtc) columnName = "creationUtc";
			else if (sortBy == SortBy.FileUploadUtc) columnName = "uploadUtc";
			else if (sortBy == SortBy.TagCount) sortByTagCount = true;
			else if (sortBy == SortBy.Random) sortByRandom = true;
			
			if (tagsAll.Length == 0 && tagsAny.Length == 0 && tagsNone.Length == 0) {
				lastQueriedCount = GetSuccessOrDuplicateCount(importId);
				counted = true;
				
				if (columnName == "Id" && !sortByTagCount && !sortByRandom) {
					if (orderBy == OrderBy.Ascending) return colHashes.Find(Query.All(Query.Ascending), offset, count).ToList();
					else if (orderBy == OrderBy.Descending) return colHashes.Find(Query.All(Query.Descending), offset, count).ToList();
					else return null; // default/placeholder, should not be called yet
				}
			}

			// var rng = new Random(); // for SortBy.Random (if I can figure out how to do so)
			var query = colHashes.Query();
			
			if (importId != "All") query = query.Where(x => x.imports.Contains(importId));
			if (groupId != "") query = query.Where(x => x.groups.Contains(groupId));
			
			if (tagsAll.Length > 0) foreach (string tag in tagsAll) query = query.Where(x => x.tags.Contains(tag));
			if (tagsAny.Length > 0) query = query.Where("$.tags ANY IN @0", BsonMapper.Global.Serialize(tagsAny));
			if (tagsNone.Length > 0) foreach (string tag in tagsNone) query = query.Where(x => !x.tags.Contains(tag));
			
			//if (countResults && !counted) lastQueriedCount = query.Count(); // slow
			
			if (sortByTagCount) {
				if (orderBy == OrderBy.Ascending) query = query.OrderBy(x => x.tags.Count);
				else if (orderBy == OrderBy.Descending) query = query.OrderByDescending(x => x.tags.Count);
				else return null; // default/placeholder, should not be called yet
			} else if (sortByRandom) {
				// not sure yet
			} else {
				if (orderBy == OrderBy.Ascending) query = query.OrderBy(columnName);
				else if (orderBy == OrderBy.Descending) query = query.OrderByDescending(columnName);
				else return null; // default/placeholder, should not be called yet
			}
			
			/* only current hope for supporting tags formatted as
					[[A,B],[C,D]]  :ie:  (A && B) || (C && D) 
				is PredicateBuilder (which is slower I believe) */
			
			var list = query.Skip(offset).Limit(count).ToList();
			lastQueriedCount = list.Count;		
			//GD.Print(list.Count);
			return list;
		} catch (Exception ex) { GD.Print("Database::_QueryDatabase() : ", ex); return null; }
	}
	private const int colorSimilarity = 0;
	private const int differenceSimilarity = 1;
	private const int averageSimilarity = 2;
	// this method is much slower than query method above, use only for similarity (would like to find another way)
	// this method will not filter out tags at the current time, import tabs/all do work though
	private List<HashInfo> _QueryBySimilarity(string importId, float[] colorHash, ulong diffHash, int offset, int count, int orderBy = OrderBy.Descending, int similarityMode=averageSimilarity)
	{
		/*if (similarityMode == averageSimilarity)
			return colHashes.Find(Query.All())
				.Where(x => importId == "All" || x.imports.Contains(importId))
				.OrderByDescending(x => (ColorSimilarity(x.colorHash, colorHash)+DifferenceSimilarity(x.diffHash, diffHash))/2.0)
				.Skip(offset)
				.Take(count)
				.ToList();
		if (similarityMode == colorSimilarity)
			return colHashes.Find(Query.All())
				.Where(x => importId == "All" || x.imports.Contains(importId))
				.OrderByDescending(x => ColorSimilarity(x.colorHash, colorHash))
				.Skip(offset)
				.Take(count)
				.ToList();
		if (similarityMode == differenceSimilarity)
			return colHashes.Find(Query.All())
				.Where(x => importId == "All" || x.imports.Contains(importId))
				.OrderByDescending(x => DifferenceSimilarity(x.diffHash, diffHash))
				.Skip(offset)
				.Take(count)
				.ToList();*/
		if (orderBy == OrderBy.Descending)
			return colHashes.Find(Query.All())
				.Where(x => importId == "All" || x.imports.Contains(importId))
				.OrderByDescending(x => 
					(similarityMode == averageSimilarity) ? 
						(ColorSimilarity(x.colorHash, colorHash)+DifferenceSimilarity(x.diffHash, diffHash))/2.0 : 
						(similarityMode == differenceSimilarity) ?
							DifferenceSimilarity(x.diffHash, diffHash) :
							ColorSimilarity(x.colorHash, colorHash))
				.Skip(offset)
				.Take(count)
				.ToList();
		else
			return colHashes.Find(Query.All())
				.Where(x => importId == "All" || x.imports.Contains(importId))
				.OrderBy(x => 
					(similarityMode == averageSimilarity) ? 
						(ColorSimilarity(x.colorHash, colorHash)+DifferenceSimilarity(x.diffHash, diffHash))/2.0 : 
						(similarityMode == differenceSimilarity) ?
							DifferenceSimilarity(x.diffHash, diffHash) :
							ColorSimilarity(x.colorHash, colorHash))
				.Skip(offset)
				.Take(count)
				.ToList();
	}
	

/*=========================================================================================
								 Data Structure Access
=========================================================================================*/
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
	
	public string[] GetPaths(string imageHash)
	{
		try {
			if (dictHashes.ContainsKey(imageHash))
				return dictHashes[imageHash].paths.ToArray();
			var result = colHashes.FindById(imageHash);
			if (result == null) return new string[0];
			return result.paths.ToArray();
		} catch (Exception ex) { return new string[0]; }
	}
	
	public bool ImportFinished(string importId)
	{
		ImportInfo importInfo;
		bool result = dictImports.TryGetValue(importId, out importInfo);
		if (!result) return false;
		return importInfo.finished;
		//return (dictImports.ContainsKey(importId)) ? dictImports[importId].finished : false;
	}
	
/*=========================================================================================
									   Similarity
=========================================================================================*/	
	public float ColorSimilarity(float[] h1, float[] h2)
	{
		int numColors = h1.Length, same = 0;
		float difference = 0f;
		
		for (int color = 0; color < numColors; color++) {
			float percent1 = h1[color], percent2 = h2[color];
			difference += Math.Abs(percent1-percent2);
			if (percent1 > 0 && percent2 > 0) same++;
		}
		
		float p1 = (float)same/numColors;
		float p2 = 1f-difference;
		
		return 50 * (p1+p2);
	}
	
	public double DifferenceSimilarity(ulong h1, ulong h2)
	{
		return CompareHash.Similarity(h1, h2);
	}
	
}
