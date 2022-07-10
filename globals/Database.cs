using Godot;
using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using Alphaleonis.Win32.Filesystem;
using LiteDB;

using System.Drawing;
using CoenM.ImageHash;
using CoenM.ImageHash.HashAlgorithms;

// query by similarity should open a new tab specifically for that purpose (similarity tab, can then further limit it to first X or simi > n%) (can also filter by tags and change order (but not sort))
// this way I also do not need to include diffHash and colorHash inside groups/imports


// I think I will remove lists of hashes from every class, now should query directly over hashInfo to find things with specific groupIds/importIds/tagIds/etc

public class ImageType
{
	// primarily for types with better built-in support, any random types and those the user adds will be assigend 'other'
	public const int jpg = 0;
	public const int png = 1;
	// ...
	public const int other = 7;
}

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


public class HashInfo
{
	public string imageHash { get; set; }			// the komi64 hash of the image (may use SHA512/256 instead)
	public string gobPath { get; set; }				// the path the file uses if it is copied/moved by the program to a central location
	
	public string diffHash { get; set; }			// the CoenM.ImageHash::DifferenceHash() of the thumbnail
	public int[] colorHash { get; set; }			// the ColorHash() of the thumbnail
	
	public int flags { get; set; }					// a FLAG integer used for toggling filter, etc
	public string thumbnailType { get; set; }		// jpg/png
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
									   Variables
=========================================================================================*/
	public Node globals;
	public bool useJournal = true;
	public string metadataPath;
	public void SetMetadataPath(string path) { metadataPath = path; }
	
	public LiteDatabase dbHashes, dbImports, dbGroups, dbTags;
	public ILiteCollection<HashInfo> colHashes;
	public ILiteCollection<ImportInfo> colImports;
	public ILiteCollection<GroupInfo> colGroups;
	public ILiteCollection<TagInfo> colTags;
	
	public Dictionary<string, HashInfo> dictHashes = new Dictionary<string, HashInfo>();
	public Dictionary<string, ImportInfo> dictImports = new Dictionary<string, ImportInfo>();
	public Dictionary<string, GroupInfo> dictGroups = new Dictionary<string, GroupInfo>();
	public Dictionary<string, TagInfo> dictTags = new Dictionary<string, TagInfo>();
	
/*=========================================================================================
									 Initialization
=========================================================================================*/
	public override void _Ready() 
	{
		globals = (Node) GetNode("/root/Globals");
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
	
	public int GetImportSuccessCount(string importId)
	{
		try {
			var tmp = colImports.FindById(importId);
			if (tmp == null) return 0;
			return tmp.successCount;
		} catch (Exception ex) { return 0; }
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
				lastQueriedCount = GetImportSuccessCount(importId);
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
			
			if (countResults && !counted) lastQueriedCount = query.Count(); // slow
			
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
			
			return query.Skip(offset).Limit(count).ToList();			
		} catch (Exception ex) { GD.Print("Database::_QueryDatabase() : ", ex); return null; }
	}

/*=========================================================================================
								 Data Structure Access
=========================================================================================*/
	public string GetFileType(string imageHash) 
	{
		return dictHashes.ContainsKey(imageHash) ? dictHashes[imageHash].thumbnailType : "";
	}
	// returning long would be better, but godot does not marshal long into its 64bit integer type for some reason
	// instead return as string, convert with .to_int() and convert to size with String.humanize_size(int)
	public string GetFileSize(string imageHash) 
	{
		return dictHashes.ContainsKey(imageHash) ? dictHashes[imageHash].size.ToString() : "";
	}

/*=========================================================================================
									   Hashing
=========================================================================================*/
	public string SHA256Hash(string path) 
	{
		return (string)globals.Call("get_sha256", path);
	}
	public string SHA512Hash(string path)
	{
		return (string)globals.Call("get_sha512", path);
	}
	public string KomiHash(string path) 
	{
		return (string)globals.Call("get_komi_hash", path);
	}
	public int[] ColorHash(string path, int accuracy=1)
	{
	// made this up as I went, took a large number of iterations but it works pretty well
	// hash: ~4x faster than DifferenceHash, simi: ~55x slower than DifferenceHash (still ~0.6s/1M comparisons though)
		int[] colors = new int[256/accuracy];
		//int[] colors = new int[766]; // orig
		var bm = new Bitmap(@path, true);
		for (int w = 0; w < bm.Width; w++) {
			for (int h = 0; h < bm.Height; h++) {
				var pixel = bm.GetPixel(w, h);
				//int color = pixel.R + pixel.G + pixel.B; // orig
				int min_color = Math.Min(pixel.B, Math.Min(pixel.R, pixel.G));
				int max_color = Math.Max(pixel.B, Math.Max(pixel.R, pixel.G));
				int color1 = ((min_color/Math.Max(max_color, 1)) * (pixel.R+pixel.G+pixel.B) * pixel.A)/(766*accuracy); 
				int color2 = (w/bm.Width) * (h/bm.Height) * ((min_color/Math.Max(max_color, 1)) * (pixel.R+pixel.G+pixel.B) * pixel.A)/(766*accuracy); 
				int color3 = (pixel.R+pixel.G+pixel.B)/(3*accuracy);
				int color = (color1+color2+color3)/(3*accuracy);
				colors[color]++;
			}
		}
		return colors;
	}
	public float ColorSimilarity(int[] h1, int[] h2)
	{
		float sum = 0f;
		int count = 0, same = 0, num1 = 0, num2 = 0;
		
		for (int color = 0; color < h1.Length; color++) {
			int sum1 = h1[color], sum2 = h2[color];
			if (sum1 > 0) {
				if (sum2 > 0) {
					same++;
					num2++;
					sum += (sum1 > sum2) ? (float)sum2/sum1 : (float)sum1/sum2;
				}
				num1++;
				count++;
			}
			else if (h2[color] > 0) num2++;
		}
		
		if (num1 == 0 && num2 == 0) return 0f;
		
		float p1 = (num1 > num2) ? (float)num2/num1 : (float)num1/num2;
		float p2 = same/((num1+num2)/2f);
		float p3 = sum/(float)count;
		
		return 100*(p1*p2+p3)/2f;
	}
	public ulong DifferenceHash(string path)
	{
		try {
			var stream = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(path);
			var algo = new DifferenceHash(); // PerceptualHash, DifferenceHash, AverageHash
			return algo.Hash(stream);
		} catch (Exception ex) { GD.Print("Database::GetDifferenceHash() : ", ex); return 0; }
	}
	public double DifferenceSimilarity(ulong h1, ulong h2)
	{
		return CompareHash.Similarity(h1, h2);
	}
	
}
