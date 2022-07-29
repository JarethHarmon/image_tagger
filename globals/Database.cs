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

public class Database : Node
{

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
	public ILiteCollection<TabInfo> colTabs;
	
	public Dictionary<string, HashInfo> dictHashes = new Dictionary<string, HashInfo>();		// maybe keep an array of imageHashes for those that have changed (so can iterate them and call col.Update())
	//public Dictionary<string, ImportInfo> dictImports = new Dictionary<string, ImportInfo>();
	public ConcurrentDictionary<string, ImportInfo> dictImports = new ConcurrentDictionary<string, ImportInfo>();
	public Dictionary<string, GroupInfo> dictGroups = new Dictionary<string, GroupInfo>();
	public Dictionary<string, TagInfo> dictTags = new Dictionary<string, TagInfo>();
	public Dictionary<string, TabInfo> dictTabs = new Dictionary<string, TabInfo>();
	
	public ImageScanner iscan;
	public ImageImporter importer;
	public Node globals;
	
/*=========================================================================================
									 Initialization
=========================================================================================*/
	public override void _Ready() 
	{
		iscan = (ImageScanner) GetNode("/root/ImageScanner");
		importer = (ImageImporter) GetNode("/root/ImageImporter");
		globals = (Node) GetNode("/root/Globals");
	}
	
	public int Create() 
	{
		try {
			if (useJournal) {
				dbHashes = new LiteDatabase(metadataPath + "hash_info.db");
				BsonMapper.Global.Entity<HashInfo>().Id(x => x.imageHash);
				colHashes = dbHashes.GetCollection<HashInfo>("hashes");
				//colHashes.EnsureIndex("tags_index", "$.tags[*]", false);
				
				dbImports = new LiteDatabase(metadataPath + "import_info.db");
				BsonMapper.Global.Entity<ImportInfo>().Id(x => x.importId);
				colImports = dbImports.GetCollection<ImportInfo>("imports");
				
				dbGroups = new LiteDatabase(metadataPath + "group_info.db");
				BsonMapper.Global.Entity<GroupInfo>().Id(x => x.groupId);
				colGroups = dbGroups.GetCollection<GroupInfo>("groups");
				
				dbTags = new LiteDatabase(metadataPath + "tag_info.db");
				BsonMapper.Global.Entity<TagInfo>().Id(x => x.tagId);
				colTags = dbTags.GetCollection<TagInfo>("tags");
				
				BsonMapper.Global.Entity<TabInfo>().Id(x => x.tabId);
				colTabs = dbImports.GetCollection<TabInfo>("tabs");				
			} 
			return 0;
		} 
		catch (Exception ex) { GD.Print("Database::Create() : ", ex); return 1; }
	}
	
	public void LoadInProgressPaths()
	{
		/*var now = DateTime.Now;
		GD.Print(colHashes.Count());
		GD.Print(DateTime.Now-now);*/
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
			}
		}
	}
	// should be called when an import finishes
	public void ClearInProgressArrays(string importId)
	{
		try {
			var importInfo = colImports.FindById(importId);
			if (importInfo == null) return;
			importInfo.inProgressPaths = null;
			importInfo.inProgressTimes = null;
			importInfo.inProgressSizes = null;
			importInfo.importedHashes = null;
			colImports.Update(importInfo);
		} catch (Exception ex) { GD.Print("Database::ClearInProgressArrays() : ", ex); return; }
	}
	
	// should be called when an import starts
	public void UploadImportArrays(string importId)
	{
		try {
			if (!dictImports.ContainsKey(importId)) return;
			var importInfo = dictImports[importId];
			var fileArray = iscan.GetInProgressPaths(importId);
			var paths = new List<string>();
			var times = new List<long>();
			var sizes = new List<long>();
			
			foreach ((string,long,long) file in fileArray) {
				paths.Add(file.Item1);
				times.Add(file.Item2);
				sizes.Add(file.Item3);
			}
			
			importInfo.inProgressPaths = paths.ToArray();
			importInfo.inProgressTimes = times.ToArray();
			importInfo.inProgressSizes = sizes.ToArray();
			importInfo.importedHashes = importer.GetImportedHashes(importId);
			colImports.Update(importInfo);			
		} catch (Exception ex) { GD.Print("Database:UploadImportArrays() : ", ex); return; }
	}
	
	// should be called when program is about to exit
	public void SaveInProgressPaths()
	{
		try {
			foreach (string iid in dictImports.Keys) {
				var iinfo = GetImport(iid);
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
			colImports.Update(GetImport("All"));		
		} catch (Exception ex) { GD.Print("Database::SaveInProgressPaths() : ", ex); return; }	
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
								  Imports Database Access
=========================================================================================*/
	
	public int GetSuccessOrDuplicateCount(string importId)
	{ 
		ImportInfo importInfo;
		bool success = dictImports.TryGetValue(importId, out importInfo);
		if (importId.Equals("All"))
			return (success) ? importInfo.successCount : 0; 
		return (success) ? importInfo.successCount + importInfo.duplicateCount : 0;
	}
	public int GetSuccessCount(string importId)
	{
		ImportInfo importInfo;
		bool success = dictImports.TryGetValue(importId, out importInfo);
		return (success) ? importInfo.successCount : 0;
	}
	public int GetDuplicateCount(string importId)
	{
		ImportInfo importInfo;
		bool success = dictImports.TryGetValue(importId, out importInfo);
		return (success) ? importInfo.duplicateCount : 0;
	}
	public int GetTotalCount(string importId)
	{
		ImportInfo importInfo;
		bool success = dictImports.TryGetValue(importId, out importInfo);
		return (success) ? importInfo.totalCount : 0;
	}
	public bool GetFinished(string importId)
	{
		if (importId.Equals("")) return true;
		ImportInfo importInfo;
		bool success = dictImports.TryGetValue(importId, out importInfo);
		return (success) ? importInfo.finished : true;
	}
	public ImportInfo GetImport(string importId)
	{
		ImportInfo importInfo = null;
		dictImports.TryGetValue(importId, out importInfo);
		return importInfo;
	}
	public string GetName(string tabId)
	{
		return (dictTabs.ContainsKey(tabId)) ? (dictTabs[tabId].tabName == null) ? "Import" : dictTabs[tabId].tabName : "Import";
	}
	public string[] GetImportIds()
	{
		return dictImports.Keys.ToArray();
	}
	public string[] GetTabIds()
	{
		return dictTabs.Keys.ToArray();
	}
	public void LoadImportInfo()
	{
		try {
			var results = colImports.FindAll();
			foreach (ImportInfo importInfo in results) 
				AddImport(importInfo.importId, importInfo);
		} catch (Exception ex) { GD.Print("Database::LoadImportInfo() : ", ex); return; }
	}
	public void LoadTabInfo()
	{
		try {
			var results = colTabs.FindAll();
			foreach (TabInfo tabInfo in results)
				dictTabs[tabInfo.tabId] = tabInfo;
		} catch (Exception ex) { GD.Print("Database::LoadTabInfo() : ", ex); return;  }
	}
	public void CreateAllInfo() 
	{
		try {
			var allInfo = colImports.FindById("All");
			if (allInfo == null) {
				allInfo = new ImportInfo {
					importId = "All",
					successCount = 0,
					failedCount = 0,
					removedCount = 0,
					finished = true,
					importTime = 0,
				};
				colImports.Insert(allInfo);
			}
			AddImport("All", allInfo);
			var tabInfo = colTabs.FindById("All");
			if (tabInfo == null) {
				tabInfo = new TabInfo {
					tabId = "All",
					tabType = 0, // may need to change eventually
					tabName = "All",
					importId = "All",
				};
				colTabs.Insert(tabInfo);
			}
			dictTabs["All"] = tabInfo;
		} catch(Exception ex) { GD.Print("Database::CreateAllInfo() : ", ex); return; }
	}
	public void RemoveTab(string tabId)
	{
		try {
			dictTabs.Remove(tabId);
			colTabs.Delete(tabId);
		} catch (Exception ex) { GD.Print("Database::RemoveTab() : ", ex); return; }
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
			if (!_importId.Equals("")) CreateImport(_importId, totalCount);	
		} catch (Exception ex) { GD.Print("Database::CreateTab() : ", ex); return; }
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
	public void CreateImport(string _importId, int _totalCount) 
	{
		try {
			var importInfo = new ImportInfo {
				importId = _importId,
				successCount = 0,
				ignoredCount = 0,
				failedCount = 0,
				duplicateCount = 0,
				removedCount = 0,
				totalCount = _totalCount,
				finished = false,
				importTime = DateTime.Now.Ticks,
			};
			AddImport(_importId, importInfo);
			colImports.Insert(importInfo);
		} catch (Exception ex) { GD.Print("Database::CreateImport() : ", ex); return; }
	}
	public void AddImport(string importId, ImportInfo importInfo)
	{
		bool result = dictImports.TryAdd(importId, importInfo);
		if (!result) {
			ImportInfo temp;
			result = dictImports.TryGetValue(importId, out temp);
			result = dictImports.TryUpdate(importId, importInfo, temp);
		}
	}
	public void UpdateImportCount(string importId, int countResult) 
	{
		try {
			var importInfo = GetImport(importId);
			var allInfo = GetImport("All");
			
			if (countResult == (int)ImportCode.SUCCESS) {
				importInfo.successCount++;
				allInfo.successCount++;
			} 
			else if (countResult == (int)ImportCode.DUPLICATE) importInfo.duplicateCount++;
			else if (countResult == (int)ImportCode.IGNORED) importInfo.ignoredCount++;
			else { 
				importInfo.failedCount++;
				allInfo.failedCount++;
			}
			AddImport("All", allInfo);
			AddImport(importId, importInfo);
			colImports.Update(allInfo);
			colImports.Update(importInfo);
		} catch(Exception ex) { GD.Print("Database::UpdateImportCount() : ", ex); return; }
	}
	public void FinishImport(string importId)
	{
		try {
			var importInfo = GetImport(importId);
			var allInfo = GetImport("All");
			importInfo.finished = true;
			AddImport(importId, importInfo);
			colImports.Update(importInfo);
			colImports.Update(allInfo);
			ClearInProgressArrays(importId);
		} catch(Exception ex) { GD.Print("Database::FinishImport() : ", ex); return; }
	}

/*=========================================================================================
									 Database Access
=========================================================================================*/	
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
	
	public bool CheckDuplicate(string imageHash, string importId)
	{
		try {
			if (dictHashes.ContainsKey(imageHash))
				return dictHashes[imageHash].imports.Contains(importId);
			var result = colHashes.FindById(imageHash);
			if (result == null) return false;
			return result.imports.Contains(importId);
		} catch (Exception ex) { return false; }
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
	// create a merged version of these two (or a function that can update any value in HashInfo, has default arguments, and checks that values are not default before updating them)
	public int AddPath(string imageHash, string imagePath)
	{
		try {
			var hashInfo = colHashes.FindById(imageHash);
			if (hashInfo == null) return 1;
			hashInfo.paths.Add(imagePath);
			if (dictHashes.ContainsKey(imageHash))
				dictHashes[imageHash] = hashInfo;
			colHashes.Update(hashInfo);
			return 0;
		} catch (Exception ex) { return 1; }
	}
	
	// 0 = new, 1 = no change, 2 = update, -1 = fail
	public int InsertHashInfo(string _imageHash, ulong _differenceHash, float[] _colorHash, int _flags, int _thumbnailType, int _imageType, long imageSize, long imageCreationUtc, string importId, string imagePath, int _width, int _height)
	{
		try {
			var hashInfo = colHashes.FindById(_imageHash);
			if (hashInfo == null) {
				hashInfo = new HashInfo {
					imageHash = _imageHash,
					differenceHash = _differenceHash,
					colorHash = _colorHash,
					flags = _flags,
					thumbnailType = _thumbnailType,
					imageType = _imageType,
					size = imageSize,
					width = _width,
					height = _height,
					creationTime = imageCreationUtc,
					uploadTime = DateTime.Now.Ticks,
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
	
	public bool HashDatabaseContainsImport(string imageHash, string importId)
	{
		try {
			var hashInfo = colHashes.FindById(imageHash);
			if (hashInfo == null) return false;
			if (hashInfo.imports == null) return false;
			if (hashInfo.imports.Contains(importId)) return true;
			return false;
		} catch (Exception ex) { return false; }
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
	
	public string[] QueryDatabase(string tabId, int offset, int count, string[] tagsAll, string[] tagsAny, string[] tagsNone, int sort=(int)Sort.SHA256, int order=(int)Order.ASCENDING, bool countResults=false)
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
				//string importId = GetImportId(tabId);
				string imageHash = GetSimilarityHash(tabId);
				var temp = colHashes.FindById(imageHash);
				if (temp == null) return new string[0];
				var hashInfos = _QueryBySimilarity("All", temp.colorHash, temp.differenceHash, offset, count, order, (int)Similarity.AVERAGE); 
				if (hashInfos == null) return new string[0];
				foreach (HashInfo hashInfo in hashInfos) {
					results.Add(hashInfo.imageHash);
					dictHashes[hashInfo.imageHash] = hashInfo;
				}
			}
			return results.ToArray();
		} catch (Exception ex) { GD.Print("Database::QueryDatabase() : ", ex); return new string[0]; }
	}
	// consider simplifying further by removing All from this one and creating a dedicated function for it
	// or could go the other direction and add groupId back to this (really only one line of difference as is)
	private List<HashInfo> _QueryImport(string importId, int offset, int count, string[] tagsAll, string[] tagsAny, string[] tagsNone, int sort=(int)Sort.SHA256, int order=(int)Order.ASCENDING, bool countResults=false)
	{
		try {
			bool sortByTagCount = false, sortByRandom = false, sortByDimensions = false, sortByDefaultRating = false, counted=false;
			string columnName = "_Id";

			if (sort == (int)Sort.SIZE) columnName = "size";
			else if (sort == (int)Sort.CREATION_TIME) columnName = "creationUtc";
			else if (sort == (int)Sort.UPLOAD_TIME) columnName = "uploadUtc";
			else if (sort == (int)Sort.DIMENSIONS) sortByDimensions = true;
			else if (sort == (int)Sort.TAG_COUNT) sortByTagCount = true;
			else if (sort == (int)Sort.RANDOM) sortByRandom = true;
			else if (sort == (int)Sort.DEFAULT_RATING) sortByDefaultRating = true;
			
			if (tagsAll.Length == 0 && tagsAny.Length == 0 && tagsNone.Length == 0) {
				_lastQueriedCount = (importId.Equals("All")) ? GetSuccessCount(importId) : GetSuccessOrDuplicateCount(importId);
				counted = true;
				
				if (columnName == "Id" && !sortByTagCount && !sortByRandom) {
					if (order == (int)Order.ASCENDING) return colHashes.Find(Query.All(Query.Ascending), offset, count).ToList();
					else if (order == (int)Order.DESCENDING) return colHashes.Find(Query.All(Query.Descending), offset, count).ToList();
					else return null; // default/placeholder, should not be called yet
				}
			}

			// var rng = new Random(); // for SortBy.Random (if I can figure out how to do so)
			var query = colHashes.Query();
			
			if (importId != "All") query = query.Where(x => x.imports.Contains(importId));
			//if (groupId != "") query = query.Where(x => x.groups.Contains(groupId));
			
			if (tagsAll.Length > 0) foreach (string tag in tagsAll) query = query.Where(x => x.tags.Contains(tag));
			if (tagsAny.Length > 0) query = query.Where("$.tags ANY IN @0", BsonMapper.Global.Serialize(tagsAny));
			if (tagsNone.Length > 0) foreach (string tag in tagsNone) query = query.Where(x => !x.tags.Contains(tag));
			
			if (countResults && !counted) _lastQueriedCount = query.Count(); // slow
			
			if (sortByTagCount) {
				if (order == (int)Order.ASCENDING) query = query.OrderBy(x => x.tags.Count);
				else if (order == (int)Order.DESCENDING) query = query.OrderByDescending(x => x.tags.Count);
				else return null; // default/placeholder, should not be called yet
			} else if (sortByRandom) {
				// not sure yet
			} else if (sortByDimensions) {
				if (order == (int)Order.ASCENDING) query = query.OrderBy(x => x.width * x.height);
				else if (order == (int)Order.DESCENDING) query = query.OrderByDescending(x => x.width * x.height);
				else return null;
			} else if (sortByDefaultRating) {
				if (order == (int)Order.ASCENDING) query = query.OrderBy(x => x.ratings["Default"]);
				else if (order == (int)Order.DESCENDING) query = query.OrderByDescending(x => x.ratings["Default"]);
				else return null;
			} else {
				if (order == (int)Order.ASCENDING) query = query.OrderBy(columnName);
				else if (order == (int)Order.DESCENDING) query = query.OrderByDescending(columnName);
				else return null; // default/placeholder, should not be called yet
			}
			
			/* only current hope for supporting tags formatted as
					[[A,B],[C,D]]  :ie:  (A && B) || (C && D) 
				is PredicateBuilder (which is slower I believe) */
			
			var list = query.Skip(offset).Limit(count).ToList();
			//_lastQueriedCount = list.Count;		
			//GD.Print(list.Count);
			return list;
		} catch (Exception ex) { GD.Print("Database::_QueryDatabase() : ", ex); return null; }
	}

	// this method is much slower than query method above, use only for similarity (would like to find another way)
	// this method will not filter out tags at the current time, import tabs/all do work though
	private List<HashInfo> _QueryBySimilarity(string importId, float[] colorHash, ulong diffHash, int offset, int count, int order=(int)Order.DESCENDING, int similarityMode=(int)Similarity.AVERAGE)
	{
		_lastQueriedCount = (importId.Equals("All")) ? GetSuccessCount(importId) : GetSuccessOrDuplicateCount(importId);
		if (order == (int)Order.DESCENDING)
			return colHashes.Find(Query.All())
				.Where(x => importId == "All" || x.imports.Contains(importId))
				.OrderByDescending(x => 
					(similarityMode == (int)Similarity.AVERAGE) ? 
						(ColorSimilarity(x.colorHash, colorHash)+DifferenceSimilarity(x.differenceHash, diffHash))/2.0 : 
						(similarityMode == (int)Similarity.DIFFERENCE) ?
							DifferenceSimilarity(x.differenceHash, diffHash) :
							ColorSimilarity(x.colorHash, colorHash))
				.Skip(offset)
				.Take(count)
				.ToList();
		else
			return colHashes.Find(Query.All())
				.Where(x => importId == "All" || x.imports.Contains(importId))
				.OrderBy(x => 
					(similarityMode == (int)Similarity.AVERAGE) ? 
						(ColorSimilarity(x.colorHash, colorHash)+DifferenceSimilarity(x.differenceHash, diffHash))/2.0 : 
						(similarityMode == (int)Similarity.DIFFERENCE) ?
							DifferenceSimilarity(x.differenceHash, diffHash) :
							ColorSimilarity(x.colorHash, colorHash))
				.Skip(offset)
				.Take(count)
				.ToList();
	}
	
	public void BulkAddTags(string[] imageHashes, string[] tags)
	{
		try {
			// this would be an OR query, which is difficult without predicate builder
			//var query = colHashes.Query()
			//query = query.Where() // imageHashes.Any(x => x.imageHash) idek
			var list = new List<HashInfo>();
			foreach (string imageHash in imageHashes) {
				var tmp = colHashes.FindById(imageHash);
				if (tmp == null) continue;
				if (tmp.tags == null) tmp.tags = new HashSet<string>(tags);
				else foreach (string tag in tags) tmp.tags.Add(tag);
				if (dictHashes.ContainsKey(imageHash)) dictHashes[imageHash] = tmp;
				list.Add(tmp);
			}
			dbHashes.BeginTrans();
			foreach (HashInfo hashInfo in list) 
				colHashes.Update(hashInfo);
			dbHashes.Commit();
		} catch (Exception ex) { GD.Print("Database::BulkAddTags() : ", ex); return; }
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
			dbHashes.BeginTrans();
			foreach (HashInfo hashInfo in list)
				colHashes.Update(hashInfo);
			dbHashes.Commit();
		} catch (Exception ex) { GD.Print("Database::BulkRemoveTags() : ", ex); return; }
	}
	
/*=========================================================================================
								 Data Structure Access
=========================================================================================*/
	public string[] GetHashes()
	{
		return dictHashes.Keys.ToArray();
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
		} catch { return new float[0]; }
	}
	public string GetCreationTime(string imageHash)
	{
		try {
			if (dictHashes.ContainsKey(imageHash))
				return  new DateTime(dictHashes[imageHash].creationTime).ToString();
			return "";
		} catch { return ""; }
	}
	
	public bool ImportFinished(string importId)
	{
		ImportInfo importInfo;
		bool result = dictImports.TryGetValue(importId, out importInfo);
		if (!result) return false;
		return importInfo.finished;
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
	
	public float GetAverageSimilarityTo(string compareHash, string imageHash)
	{
		var hashInfo1 = (dictHashes.ContainsKey(compareHash)) ? dictHashes[compareHash] : colHashes.FindById(compareHash);
		var hashInfo2 = (dictHashes.ContainsKey(imageHash)) ? dictHashes[imageHash] : colHashes.FindById(imageHash);
		float color = ColorSimilarity(hashInfo1.colorHash, hashInfo2.colorHash);
		double difference = DifferenceSimilarity(hashInfo1.differenceHash, hashInfo2.differenceHash);
		return (color+(float)difference)/2f;
	}
}
