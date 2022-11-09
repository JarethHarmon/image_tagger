using Godot;
using System;
using System.IO;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Windows.Forms;
using Alphaleonis.Win32.Filesystem;

// return error codes when relevant
// use AlphaFS when relevant

// need a way to interrupt the scan if the user cancels the import 
public class ImageScanner : Node
{
	public HashSet<string> extensionsToImport = new HashSet<string>{".PNG", ".JPG", ".JPEG", ".JFIF", ".APNG", ".GIF", ".BMP", ".WEBP"};
	public List<string> blacklistedFolders = new List<string>{"SYSTEM VOLUME INFORMATION", "$RECYCLE.BIN"};
	
	private string lastImportId = "";
	private Dictionary<string, HashSet<string>> tempPaths = new Dictionary<string, HashSet<string>>();
	private HashSet<(string,string,long,long)> pathListTempFiles = new HashSet<(string,string,long,long)>();
	private HashSet<(string,string,long,long)> returnedTempFiles = new HashSet<(string,string,long,long)>();
	
	private Godot.Label currentPathDisplay;
	public void SetCurrentPathDisplay(string path)
	{
		currentPathDisplay = GetNode<Godot.Label>(@path);
	}

	private bool cancel = false;
	public void Cancel() { 
		cancel = true;
		Clear();
	}

	public int ScanFiles(string[] filePaths, string importId)
	{
		pathListTempFiles.Clear();
		cancel = false;
		int imageCount = 0;
		try {
			lastImportId = importId;
			foreach (string path in filePaths) {
				if (cancel) return 0;
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
			if (cancel) return 0;
			return imageCount;
		}
		catch (Exception ex) { GD.Print("ImageScanner::ScanFiles() : ", ex); return imageCount; }
	}
	
	public int ScanDirectories(string path, bool recursive, string importId)
	{
		try {
			cancel = false;
			lastImportId = importId;
			var dirInfo = new System.IO.DirectoryInfo(@path);
			int imageCount = _ScanDirectories(dirInfo, recursive, importId);
			if (cancel) return 0;
			return imageCount;
		}
		catch (Exception ex) { GD.Print("ImageScanner::ScanDirectories() : ", ex); return 0; }
	}
	
	private int _ScanDirectories(System.IO.DirectoryInfo dirInfo, bool recursive, string importId)
	{
		int imageCount = 0;
		try {	
			cancel = false;
			this.CallDeferred(nameof(SetCurrentPathDisplayText), dirInfo.FullName);
			var _paths = new HashSet<string>();
			foreach (System.IO.FileInfo fileInfo in dirInfo.GetFiles()) {
				if (cancel) return 0;
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
				if (cancel) return 0;
				if (!dir.FullName.Contains(" ")) { // U+00A0 (this symbol really breaks things for some reason)
					foreach (string folder in blacklistedFolders)
						if (dir.FullName.Contains(folder)) // maybe change to only check current folder and use == / .Equals()
							continue; 
					imageCount += _ScanDirectories(dir, recursive, importId);
				}
			}
			if (cancel) return 0;
			return imageCount;
		} 
		catch (Exception ex) { GD.Print("ImageScanner::_ScanDirectories() : ", ex); return imageCount; }
	}
	
	private void SetCurrentPathDisplayText(string path)
	{
		try {
			currentPathDisplay.Text = path;
		}
		catch (Exception ex) {
			GD.Print(ex);
			return;
		}
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
