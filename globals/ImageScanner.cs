using Godot;
using System;
using System.IO;
using System.Collections.Generic;
using System.Windows.Forms;
using Alphaleonis.Win32.Filesystem;

public class ImageScanner : Node
{
	public HashSet<string> extensionsToImport = new HashSet<string>{".PNG", ".JPG", ".JPEG"};
	public List<string> blacklistedFolders = new List<string>{"SYSTEM VOLUME INFORMATION", "$RECYCLE.BIN"};
	
	// importing will need to access these and then call clear()
	//private HashSet<IEnumerable<System.IO.DirectoryInfo>> folders = new HashSet<IEnumerable<System.IO.DirectoryInfo>>();
	private Dictionary<string, HashSet<(string, string, long, long)>> files = new Dictionary<string, HashSet<(string, string, long, long)>>();
	
	// stores most recently scanned files
	private HashSet<(string,string,long,long)> tempFiles = new HashSet<(string,string,long,long)>();
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
	
	public int ScanFiles(string[] filePaths)
	{
		tempFiles.Clear();
		int imageCount = 0;
		try {
			foreach (string path in filePaths) {
				var fileInfo = new System.IO.FileInfo(@path);
				if (extensionsToImport.Contains(fileInfo.Extension.ToUpperInvariant())) { 
					string dir = fileInfo.Directory.FullName.Replace("\\", "/");
					if (files.ContainsKey(dir)) {
						files[dir].Add(((fileInfo.Name, fileInfo.Extension, fileInfo.CreationTimeUtc.Ticks, fileInfo.Length)));
						tempFiles.Add((fileInfo.FullName, fileInfo.Extension, fileInfo.CreationTimeUtc.Ticks, fileInfo.Length));
					} else {
						files[dir] = new HashSet<(string,string,long,long)>{(fileInfo.Name, fileInfo.Extension, fileInfo.CreationTimeUtc.Ticks, fileInfo.Length)};
						tempFiles.Add((fileInfo.FullName, fileInfo.Extension, fileInfo.CreationTimeUtc.Ticks, fileInfo.Length));
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
	public int ScanDirectories(string path, bool recursive)
	{
		tempFiles.Clear();
		try {
			var dirInfo = new System.IO.DirectoryInfo(@path);
			int imageCount = _ScanDirectories(dirInfo, recursive);
			return imageCount;
		}
		catch (Exception ex) { GD.Print("ImageScanner::ScanDirectories() : ", ex); return 0; }
	}
	
	private int _ScanDirectories(System.IO.DirectoryInfo dirInfo, bool recursive)
	{
		int imageCount = 0;
		try {	
			var _files = new HashSet<(string, string, long, long)>();
			foreach (System.IO.FileInfo fileInfo in dirInfo.GetFiles()) {
				if (extensionsToImport.Contains(fileInfo.Extension.ToUpperInvariant())) {
					_files.Add((fileInfo.Name, fileInfo.Extension, fileInfo.CreationTimeUtc.Ticks, fileInfo.Length));
					tempFiles.Add((fileInfo.FullName, fileInfo.Extension, fileInfo.CreationTimeUtc.Ticks, fileInfo.Length));
					imageCount++;
				}
			}
			
			files[dirInfo.FullName.Replace("\\", "/")] = _files;
			if (recursive == false) return imageCount;
			
			var enumeratedDirectories = dirInfo.EnumerateDirectories();
			//folders.Add(enumeratedDirectories);
			foreach (System.IO.DirectoryInfo dir in enumeratedDirectories) {
				if (!dir.FullName.Contains(" ")) { // U+00A0 (this symbol really breaks things for some reason)
					foreach (string folder in blacklistedFolders)
						if (dir.FullName.Contains(folder)) // maybe change to only check current folder and use == / .Equals()
							continue; 
					imageCount += _ScanDirectories(dir, recursive);
				}
			}
			return imageCount;
		} 
		catch (Exception ex) { GD.Print("ImageScanner::_ScanDirectories() : ", ex); return imageCount; }
	}
	
	public List<(string, string, long, long)> GetImages() 
	{
		var images = new List<(string, string, long, long)>();
		foreach (string folder in files.Keys) 
			foreach ((string, string, long, long) file in files[folder])
				images.Add((folder + "/" + file.Item1, file.Item2, file.Item3, file.Item4));
		_Clear();
		return images;
	}
	
	public string[] GetPathsSizes()
	{
		var results = new List<string>();
		foreach ((string, string, long, long) file in tempFiles) {
			if (!returnedTempFiles.Contains(file)) {
				returnedTempFiles.Add(file);
				results.Add((file.Item1 + "?" + file.Item4.ToString()).Replace("\\", "/"));
			}
		}
		return results.ToArray();
	}
	
	// called by ImageImporter after retrieving the images to import
	private void _Clear()
	{
		files.Clear();
		//folders.Clear();
		tempFiles.Clear();
		returnedTempFiles.Clear();
	}
	
}
