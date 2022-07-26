using Godot;
using System;
using System.Collections.Generic;

namespace Data
{
	public enum ImportCode { SUCCESS, DUPLICATE, IGNORED, FAILED }
	public enum ImageType { JPG, PNG, APNG, OTHER=7, ERROR }  
	public enum Sort { SHA256, PATH, SIZE, CREATION_TIME, UPLOAD_TIME, DIMENSIONS, TAG_COUNT, RANDOM, DEFAULT_RATING }
	public enum Order { ASCENDING, DESCENDING }
	public enum Tab { IMPORT_GROUP, IMAGE_GROUP, TAG, SIMILARITY }
	public enum Similarity { COLOR, DIFFERENCE, AVERAGE }
	
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
}
