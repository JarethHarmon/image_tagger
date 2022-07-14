using Godot;
using System;
using System.IO;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Windows.Forms;
using Alphaleonis.Win32.Filesystem;

public class ImageScanner : Node
{
	public HashSet<string> extensionsToImport = new HashSet<string>{".PNG", ".JPG", ".JPEG"};
	public List<string> blacklistedFolders = new List<string>{"SYSTEM VOLUME INFORMATION", "$RECYCLE.BIN"};
	
	private ConcurrentDictionary<string, ConcurrentQueue<(string,long,long)>> files = new ConcurrentDictionary<string, ConcurrentQueue<(string,long,long)>>();
	
	// stores most recently scanned files
	private string lastImportId = "";
	private Dictionary<string, HashSet<(string, string, long, long)>> tempFiles = new Dictionary<string, HashSet<(string, string, long, long)>>();
	private HashSet<(string,string,long,long)> pathListTempFiles = new HashSet<(string,string,long,long)>();
	private HashSet<(string,string,long,long)> returnedTempFiles = new HashSet<(string,string,long,long)>();
	
	/*public void OpenFileBrowser()
	{
		var fileDialog = new System.Windows.Forms.OpenFileDialog();
		fileDialog.InitialDirectory = @"C:/"; // set based on Globals.settings
		fileDialog.Title = "Choose Folders or Images";
		fileDialog.Filter = "image files (*.jpg,*.jpeg,*.png)|*.jpg;*.jpeg;*.png|all files (*.*)|*.*";
		fileDialog.Multiselect = true;
		if (fileDialog.ShowDialog() == DialogResult.OK) {
			foreach (string file in fileDialog.FileNames) {
				GD.Print(file);
			}
		}
	}*/
	
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
					if (tempFiles.ContainsKey(dir)) {
						tempFiles[dir].Add(((fileInfo.Name, fileInfo.Extension, fileInfo.CreationTimeUtc.Ticks, fileInfo.Length)));
						pathListTempFiles.Add((fileInfo.FullName, fileInfo.Extension, fileInfo.CreationTimeUtc.Ticks, fileInfo.Length));
					} else {
						tempFiles[dir] = new HashSet<(string,string,long,long)>{(fileInfo.Name, fileInfo.Extension, fileInfo.CreationTimeUtc.Ticks, fileInfo.Length)};
						pathListTempFiles.Add((fileInfo.FullName, fileInfo.Extension, fileInfo.CreationTimeUtc.Ticks, fileInfo.Length));
					}
					imageCount++;
				} 
			}
			return imageCount;
		}
		catch (Exception ex) { GD.Print("ImageScanner::ScanFiles() : ", ex); return imageCount; }
	}
	
	// has no upper bounds currently, may be worth making it slower and uploading to database
	// that said, ItemList on the gdscript side uses ~2x more RAM at scale than this script does
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
			var _files = new HashSet<(string, string, long, long)>();
			foreach (System.IO.FileInfo fileInfo in dirInfo.GetFiles()) {
				if (extensionsToImport.Contains(fileInfo.Extension.ToUpperInvariant())) {
					_files.Add((fileInfo.Name, fileInfo.Extension, fileInfo.CreationTimeUtc.Ticks, fileInfo.Length));
					pathListTempFiles.Add((fileInfo.FullName, fileInfo.Extension, fileInfo.CreationTimeUtc.Ticks, fileInfo.Length));
					imageCount++;
				}
			}
			
			tempFiles[dirInfo.FullName.Replace("\\", "/")] = _files;
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
	
	public void CommitImport()
	{ 
		string importId = lastImportId;
		
		foreach (string folder in tempFiles.Keys) {
			var queue = this.files.GetOrAdd(importId, _ => new ConcurrentQueue<(string,long,long)>());
			foreach ((string,string,long,long) file in tempFiles[folder]) 
				queue.Enqueue((folder + "/" + file.Item1, file.Item3, file.Item4));
		}
		
		_Clear();
	}
	
	public (string,long,long) GetImage(string importId)
	{
		ConcurrentQueue<(string,long,long)> images;
		(string,long,long) image = ("", 0, 0);
		if (this.files.TryGetValue(importId, out images))
			images.TryDequeue(out image);
		
		return image;
	}
	
	// called by import_list.gd::create_new_import_button()
	private void _Clear()
	{
		tempFiles.Clear();
		pathListTempFiles.Clear();
		returnedTempFiles.Clear();
	}
	
}
