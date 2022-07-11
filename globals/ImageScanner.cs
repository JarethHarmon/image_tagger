using Godot;
using System;
using System.IO;
using System.Collections.Generic;
using Alphaleonis.Win32.Filesystem;

public class ImageScanner : Node
{
	public HashSet<string> extensionsToImport = new HashSet<string>{".PNG", ".JPG", ".JPEG"};
	public List<string> blacklistedFolders = new List<string>{"SYSTEM VOLUME INFORMATION", "$RECYCLE.BIN"};
	
	private List<IEnumerable<System.IO.DirectoryInfo>> folders = new List<IEnumerable<System.IO.DirectoryInfo>>();
	private Dictionary<string, List<(string, string, long, long)>> files = new Dictionary<string, List<(string, string, long, long)>>();
	
	public int ScanDirectories(string path, bool recursive)
	{
		files.Clear();
		folders.Clear();
		var dirInfo = new System.IO.DirectoryInfo(@path);
		int imageCount = _ScanDirectories(dirInfo, recursive);
		return imageCount;
	}
	
	private int _ScanDirectories(System.IO.DirectoryInfo dirInfo, bool recursive)
	{
		int imageCount = 0;
		try {	
			var _files = new List<(string, string, long, long)>();
			foreach (System.IO.FileInfo fileInfo in dirInfo.GetFiles()) {
				if (extensionsToImport.Contains(fileInfo.Extension.ToUpperInvariant())) {
					_files.Add((fileInfo.Name, fileInfo.Extension, fileInfo.CreationTimeUtc.Ticks, fileInfo.Length));
					imageCount++;
				}
			}
			
			files[dirInfo.FullName.Replace("\\", "/")] = _files;
			if (recursive == false) return imageCount;
			
			var enumeratedDirectories = dirInfo.EnumerateDirectories();
			folders.Add(enumeratedDirectories);
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
		return images;
	}
	
}