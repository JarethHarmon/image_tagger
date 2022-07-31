using Godot;
using System;
using System.IO;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Windows.Forms;
using Alphaleonis.Win32.Filesystem;

// return error codes when relevant
// use AlphaFS when relevant

public class ImageScanner : Node
{
	public HashSet<string> extensionsToImport = new HashSet<string>{".PNG", ".JPG", ".JPEG"};
	public List<string> blacklistedFolders = new List<string>{"SYSTEM VOLUME INFORMATION", "$RECYCLE.BIN"};
	
	private string lastImportId = "";
	private Dictionary<string, HashSet<string>> tempPaths = new Dictionary<string, HashSet<string>>();
	private HashSet<(string,string,long,long)> pathListTempFiles = new HashSet<(string,string,long,long)>();
	private HashSet<(string,string,long,long)> returnedTempFiles = new HashSet<(string,string,long,long)>();
	
	public int ScanFiles(string[] filePaths, string importId)
	{
		pathListTempFiles.Clear();
		int imageCount = 0;
		try {
			lastImportId = importId;
			foreach (string path in filePaths) {
				var fileInfo = new System.IO.FileInfo(@path);
				if (extensionsToImport.Contains(fileInfo.Extension.ToUpperInvariant())) { 
					string dir = fileInfo.Directory.FullName.Replace("\\", "/");
					if (tempPaths.ContainsKey(dir)) {
						tempPaths[dir].Add(fileInfo.Name);
						pathListTempFiles.Add((fileInfo.FullName, fileInfo.Extension, fileInfo.CreationTimeUtc.Ticks, fileInfo.Length));
					} else {
						tempPaths[dir] = new HashSet<string>{fileInfo.Name};
						pathListTempFiles.Add((fileInfo.FullName, fileInfo.Extension, fileInfo.CreationTimeUtc.Ticks, fileInfo.Length));
					}
					imageCount++;
				} 
			}
			return imageCount;
		}
		catch (Exception ex) { GD.Print("ImageScanner::ScanFiles() : ", ex); return imageCount; }
	}
	
	public int ScanDirectories(string path, bool recursive, string importId)
	{
		try {
			lastImportId = importId;
			var dirInfo = new System.IO.DirectoryInfo(@path);
			int imageCount = _ScanDirectories(dirInfo, recursive, importId);
			return imageCount;
		}
		catch (Exception ex) { GD.Print("ImageScanner::ScanDirectories() : ", ex); return 0; }
	}
	
	private int _ScanDirectories(System.IO.DirectoryInfo dirInfo, bool recursive, string importId)
	{
		int imageCount = 0;
		try {	
			var _paths = new HashSet<string>();
			foreach (System.IO.FileInfo fileInfo in dirInfo.GetFiles()) {
				if (extensionsToImport.Contains(fileInfo.Extension.ToUpperInvariant())) {
					_paths.Add(fileInfo.Name);
					pathListTempFiles.Add((fileInfo.FullName, fileInfo.Extension, fileInfo.CreationTimeUtc.Ticks, fileInfo.Length));
					imageCount++;
				}
			}
			
			tempPaths[dirInfo.FullName.Replace("\\", "/")] = _paths;
			if (recursive == false) return imageCount;
			
			var enumeratedDirectories = dirInfo.EnumerateDirectories();
			foreach (System.IO.DirectoryInfo dir in enumeratedDirectories) {
				if (!dir.FullName.Contains(" ")) { // U+00A0 (this symbol really breaks things for some reason)
					foreach (string folder in blacklistedFolders)
						if (dir.FullName.Contains(folder)) // maybe change to only check current folder and use == / .Equals()
							continue; 
					imageCount += _ScanDirectories(dir, recursive, importId);
				}
			}
			return imageCount;
		} 
		catch (Exception ex) { GD.Print("ImageScanner::_ScanDirectories() : ", ex); return imageCount; }
	}
	
	public string[] GetPathsSizes()
	{
		var results = new List<string>();
		foreach ((string, string, long, long) file in pathListTempFiles) {
			if (!returnedTempFiles.Contains(file)) {
				returnedTempFiles.Add(file);
				results.Add((file.Item1 + "?" + file.Item4.ToString()).Replace("\\", "/"));
			}
		}
		return results.ToArray();
	}
	
	public string[] GetPaths()
	{
		var list = new List<string>();
		foreach (string folder in tempPaths.Keys)
			foreach (string file in tempPaths[folder])
				list.Add(folder + "/" + file);
		Clear();
		return list.ToArray();
	}

	public void Clear()
	{
		tempPaths.Clear();
		pathListTempFiles.Clear();
		returnedTempFiles.Clear();
	}
}
