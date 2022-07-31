using Godot;
using System;
using System.Collections.Generic;

namespace Data
{
	public enum ErrorCodes { OK, ERROR }
	public enum ImportCode { SUCCESS, DUPLICATE, IGNORED, FAILED }
	public enum ImageType { JPG, PNG, APNG, OTHER=7, ERROR=-1 }  
	public enum Sort { SHA256, PATH, SIZE, CREATION_TIME, UPLOAD_TIME, DIMENSIONS, TAG_COUNT, RANDOM, DEFAULT_RATING }
	public enum Order { ASCENDING, DESCENDING }
	public enum Tab { IMPORT_GROUP, IMAGE_GROUP, TAG, SIMILARITY }
	public enum Similarity { COLOR, DIFFERENCE, AVERAGE }

	public class HashInfo 
	{
		public string imageHash { get; set; }                 // the SHA256 hash of the image
		public string internalPath { get; set; }              // the new path of the image if user allows copying/moving images to a central location
		public string imageName { get; set; }                 // the displayed name of the image, also used for sorting (can be manually set or can cycle through paths)  

		public ulong differenceHash { get; set; }             // the CoenM difference hash of the image
		public float[] colorHash { get; set; }                // the 'color hash' I made

		public int width { get; set; }                        // the width of the image in pixels
		public int height { get; set; }                       // the height of the image in pixels
		public int flags { get; set; }                        // the flags of the image (currently used only for filter toggling)
		public int thumbnailType { get; set; }                // the type (png/jpg/?) of the thumbnail
		public int imageType { get; set; }                    // the type (png/jpg/apng/etc) of the full image
		public long size { get; set; }                        // the size of the image in bytes
		public long creationTime { get; set; }                // the UTC time that the image was created
		public long lastWriteTime { get; set; }               // the UTC time that the image was last edited
		public long uploadTime { get; set; }                  // the UTC time that this hashInfo was first uploaded to the database
		public long lastEditTime { get; set; }                // the UTC time that this hashInfo was last changed

		public bool isGroupLeader { get; set; }               // whether this is the first(cover) image in a group (cover of a comic for example)
		public HashSet<string> imports { get; set; }          // the imports this imageHash was present in
		public HashSet<string> groups { get; set; }           // the groups this imageHash is present in
		public HashSet<string> paths { get; set; }            // the paths that this imageHash has been found at
		public HashSet<string> tags { get; set; }             // the tags that have been applied to this imageHash
		public Dictionary<string, int> ratings { get; set; }  // the user-assigned ratings for this imageHash
	}
	
	public class ImportInfo
	{
		public string importId { get; set; }	// the generated ID I... of this import
		public string importName { get; set; }	// the name assigned to this import ('Import' by default)

		public int total { get; set; }			// the number of images to be processed
		public int processed { get; set; }		// the number of image that have been processed
		public int success { get; set; }		// the number of images whose import was successful
		public int ignored { get; set; }		// the number of images whose imports were ignored (by user-defined settings or because they were already imported in this import)
		public int duplicate { get; set; }		// the number of images which have been imported in a previous import
		public int failed { get; set; }			// the number of images which failed to import for whatever reason
		public long importStart { get; set; }	// the UTC time that the import started
		public long importFinish { get; set; }	// the UTC time that the import finished

		public bool finished { get; set; }			// whether or not the import has finished
		public HashSet<string> progressIds { get; set; }	// the ids of the sectioned in-progress arrays of paths,etc for this import
	}

	public class ImportProgress
	{
		public string progressId { get; set; }	// the generated id P... of this section of the in-progress arrays
		public string[] paths { get; set; }		// the paths for this section of the in-progress arrays
	}

	public class GroupInfo
	{
		public string groupId { get; set; }		// the generated id G... of this group
		public string groupName { get; set; }	// the user-assigned name of this group (groupId by default)
		public int count { get; set; }			// how many members this group has (may remove in favor of members.Length)
		public long creationTime { get; set; }		// time that this group was created
		public long lastChangeTime { get; set; }	// last time this group had members added/removed
		public long lastEditTime { get; set; }		// last time this group was changed in any way
		public HashSet<string> tags { get; set; }	// the tags applied to this group as a whole (copied to leader/cover image)
		public string[] members { get; set; }		// ordered, duplicates and circular references allowed (legitimate use cases)
	}

	public class TabInfo
	{
		public string tabId { get; set; }		// the generated id T... of this tab
		public string tabName { get; set; }		// the displayed name of this tab  
		public int tabType { get; set; }		// identifies the type (Import/Similarity/Group/Tag/?) of this tab
		public string importId { get; set; }	// only for Import tabs
		public string groupId { get; set; }		// only for Group tabs
		public string tag { get; set; }			// only for Tag tabs
		public string similarityHash { get; set; }	// only for Similarity tabs
	}

	public class TagInfo
	{
		public string tagId { get; set; }
		public string tagName { get; set; }
		public HashSet<string> tagParents { get; set; }
	}
}

public class Storage : Node
{
	private int _maxStoredPages = 500;
	public void SetMaxStoredPages(int maxPages) { _maxStoredPages = maxPages; }
	
	private List<int> pageQueue = new List<int>();
	private Dictionary<int, string[]> storedPages = new Dictionary<int, string[]>();
	
	public void AddPage(int hashId, string[] hashes)
	{
		if (pageQueue.Count >= _maxStoredPages){
			int removedHash = pageQueue[0];
			pageQueue.RemoveAt(0);
			storedPages.Remove(removedHash);
		}
		storedPages[hashId] = hashes;
		pageQueue.Add(hashId);
	}
	public void UpdatePageQueuePosition(int hashId) 
	{
		int removeIndex = pageQueue.FindIndex(x => x == hashId);
		if (removeIndex < 0) return;
		int removedHash = pageQueue[removeIndex];
		pageQueue.RemoveAt(removeIndex);
		pageQueue.Add(removedHash);
	}
	public string[] GetPage(int hashId)
	{
		return (HasPage(hashId)) ? storedPages[hashId] : new string[0];
	}
	public bool HasPage(int hashId)
	{
		return storedPages.ContainsKey(hashId);
	}
}
