using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using Alphaleonis.Win32.Filesystem;
using CoenM.ImageHash;
using CoenM.ImageHash.HashAlgorithms;
using ImageMagick;
using Data;

public class ImageImporter : Node
{

/*=========================================================================================
									   Variables
=========================================================================================*/
	public const int MAX_PATH_LENGTH = 256, AVG_THUMBNAIL_SIZE = 7424;
	
	public Node globals, signals;
	public ImageScanner iscan;
	public Database db;
	
	public bool filterThumbnails = false;
	public string thumbnailPath;
	public void SetThumbnailPath(string path) { thumbnailPath = path; }
	
	private Dictionary<string, HashSet<string>> importedHashes = new Dictionary<string, HashSet<string>>();
	
/*=========================================================================================
									 Initialization
=========================================================================================*/
	public override void _Ready() 
	{
		globals = (Node) GetNode("/root/Globals");
		signals = (Node) GetNode("/root/Signals");
		iscan = (ImageScanner) GetNode("/root/ImageScanner");
		db = (Database) GetNode("/root/Database");
	}
	
/*=========================================================================================
										 IO
=========================================================================================*/
	public static byte[] LoadFile(string path)
	{
		if (!FileExists(path)) return new byte[0];
		return (path.Length() < MAX_PATH_LENGTH) ? System.IO.File.ReadAllBytes(path) : Alphaleonis.Win32.Filesystem.File.ReadAllBytes(path);
	} 
	public static bool FileExists(string path)
	{
		return (path.Length() < MAX_PATH_LENGTH) ? System.IO.File.Exists(path) : Alphaleonis.Win32.Filesystem.File.Exists(path);
	}
	public static bool IsImageCorrupt(string path)
	{
		try {
			var im = (path.Length() < MAX_PATH_LENGTH) ? new MagickImage(path) : new MagickImage(LoadFile(path));
			return false;
		} catch (Exception ex) { GD.Print("ImageImporter::IsImageCorrupt() : ", ex); return true; }
	}
	
	public int SaveThumbnail(string imagePath, string savePath, string imageHash, long imageSize)
	{
		// in general need to remove invalid paths whenver they are iterated
		
		return _SaveThumbnail(imagePath, savePath, imageSize);
	}
	private int _SaveThumbnail(string imagePath, string thumbPath, long imageSize)
	{
		try {
			int result = (int)ImageType.JPG; // 0 == JPG, 1 == PNG, -1 == ERR  (used to set HashInfo.thumbnailType in the Database) (need to create an Enum ideally)
			var im = (imagePath.Length() < MAX_PATH_LENGTH) ? new MagickImage(imagePath) : new MagickImage(LoadFile(imagePath));
			im.Strip();
			if (imageSize > AVG_THUMBNAIL_SIZE) {
				im.Format = MagickFormat.Jpg;
				im.Quality = 50;
				im.Resize(256, 256);
				im.Write(thumbPath);
				new ImageOptimizer().Compress(thumbPath);
			}
			else {
				im.Format = MagickFormat.Png;
				result = (int)ImageType.PNG;
				im.Write(thumbPath);
				new ImageOptimizer().LosslessCompress(thumbPath);
			}
			return result;
		} catch (Exception ex) { GD.Print("ImageImporter::_SaveThumbnail() : ", ex); return (int)ImageType.ERROR; }
	}
	
	public int GetActualFormat(string imagePath)
	{
		(string sformat, int width, int height) = _GetImageInfo(imagePath);
		int format = (int)ImageType.OTHER;
		if ((bool) globals.Call("is_apng", imagePath)) format = (int)ImageType.APNG;
		else if (sformat == "JPG") format = (int)ImageType.JPG;
		else if (sformat == "PNG") format = (int)ImageType.PNG;
		return format;
	}
	
	public (int, int, int) GetImageInfo(string imagePath)
	{
		(string sformat, int width, int height) = _GetImageInfo(imagePath);
		int format = (int)ImageType.OTHER;
		if ((bool) globals.Call("is_apng", imagePath)) format = (int)ImageType.APNG;
		else if (sformat == "JPG") format = (int)ImageType.JPG;
		else if (sformat == "PNG") format = (int)ImageType.PNG;
		
		return (format, width, height);
	}
	private static (string, int, int) _GetImageInfo(string imagePath)
	{
		try {
			var info = (imagePath.Length() < MAX_PATH_LENGTH) ? new MagickImageInfo(imagePath) : new MagickImageInfo(LoadFile(imagePath));
			string format = info.Format.ToString().ToUpperInvariant().Replace("JPEG", "JPG");
			return (format, info.Width, info.Height);
		} catch (Exception ex) { GD.Print("ImageImporter::_GetImageInfo() : ", ex); return ("", 0, 0); }
	}
	// for loading images besides bmp/jpg/png (or bmp/jpg/png that have some sort of issue)
	public static Godot.Image LoadUnsupportedImage(string imagePath, long imageSize)
	{
		try {
			var im = (imagePath.Length() < MAX_PATH_LENGTH) ? new MagickImage(imagePath) : new MagickImage(LoadFile(imagePath));
			if (imageSize > AVG_THUMBNAIL_SIZE) {
				im.Format = MagickFormat.Jpg;
				im.Quality = 95;
				byte[] data = im.ToByteArray();
				var image = new Godot.Image();
				image.LoadJpgFromBuffer(data);
				return image;
			} else {
				im.Format = MagickFormat.Png;
				im.Quality = 95;
				byte[] data = im.ToByteArray();
				var image = new Godot.Image();
				image.LoadPngFromBuffer(data);
				return image;
			}
		} catch (Exception ex) { GD.Print("ImageImporter::LoadUnsupportedImage() : ", ex); return null; }		
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
	public ulong DifferenceHash(string path)
	{
		try {
			var stream = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(path);
			var algo = new DifferenceHash(); // PerceptualHash, DifferenceHash, AverageHash
			return algo.Hash(stream);
		} catch (Exception ex) { GD.Print("Database::GetDifferenceHash() : ", ex); return 0; }
	}
	private string GetRandomID(int num_bytes)
	{
		try{
			byte[] bytes = new byte[num_bytes];
			var rng = new RNGCryptoServiceProvider();
			rng.GetBytes(bytes);
			rng.Dispose();
			return BitConverter.ToString(bytes).Replace("-", "");
		}
		catch (Exception ex) { GD.Print("Database::GetRandomID() : ", ex); return ""; } 
	}

	// get 64bit ID
	public string CreateImportID() { return "I" + GetRandomID(8); }
	public string CreateTabID() { return "T" + GetRandomID(8); }	
	public string CreateGroupID() { return "G" + GetRandomID(8); }
	public string CreateProgressID() { return "P" + GetRandomID(8); }
	
	public float[] ColorHash(string path, int bucketSize=16) 
	{
		int[] colors = new int[256/bucketSize];
		var bitmap = new Bitmap(@path, true);
		int size = bitmap.Width * bitmap.Height;
		for (int w = 0; w < bitmap.Width; w++) {
			for (int h = 0; h < bitmap.Height; h++) {
				var pixel = bitmap.GetPixel(w, h);
				int min_color = Math.Min(pixel.B, Math.Min(pixel.R, pixel.G));
				int max_color = Math.Max(pixel.B, Math.Max(pixel.R, pixel.G));
				int color1 = ((min_color/Math.Max(max_color, 1)) * (pixel.R+pixel.G+pixel.B) * pixel.A)/(766*bucketSize); 
				int color3 = (pixel.R+pixel.G+pixel.B)/(3*bucketSize);
				int color = (color1+color3)/2;
				colors[color]++;
			}
		}
		
		float[] hash = new float[256/bucketSize];
		for (int color = 0; color < colors.Length; color++) {
			hash[color] = 100 * (float)colors[color]/size;
		}
		return hash;
		
	}
	
/*=========================================================================================
									   Importing
=========================================================================================*/
	public void ImportImage(string[] tabs, string importId, string progressId, string path)
	{
		try {
			if (importId.Equals("") || progressId.Equals("") || path.Equals("")) return;
			
			int imageCount = db.GetTotalCount(importId);
			//var file = (safePathLength) ?
			//	new System.IO.FileInfo(path) : 
			//	new Alphaleonis.Win32.Filesystem.FileInfo(path);
			var file = new System.IO.FileInfo(path);
			var image = (path, file.Length, file.CreationTimeUtc.Ticks, file.LastWriteTimeUtc.Ticks);	
		
			int result = _ImportImage(image, importId, progressId, imageCount);
			db.UpdateImportCount(importId, result);
			
			if (tabs.Length > 0) 
				signals.Call("emit_signal", "increment_import_buttons", tabs);
			if (result == (int)ImportCode.SUCCESS)
				signals.Call("emit_signal", "increment_all_button");
		}
		catch (Exception ex) {
			GD.Print("ImageImporter::ImportImage() : ", ex);
			return;
		}
	}
	
	private int _ImportImage((string,long,long,long) imageInfo, string importId, string progressId, int imageCount) 
	{
		try {
			(string imagePath, long imageSize, long imageCreationUtc, long imageLastUpdateUtc) = imageInfo;
			bool safePathLength = imagePath.Length() < MAX_PATH_LENGTH;
			// checks if imagePath exists
			if ((safePathLength) ? !System.IO.File.Exists(imagePath) : !Alphaleonis.Win32.Filesystem.File.Exists(imagePath)) {
				db.IncrementFailedCount(progressId, (int)ImportCode.FAILED);
				return (int)ImportCode.FAILED; 
			}

			// check that the path/type/time/size meet the conditions specified by user (return ImportCode.IGNORED if not)

			string _imageHash = (string) globals.Call("get_sha256", imagePath); 
			string _imageName = (string) globals.Call("get_file_name", imagePath);

			// checks if the current import has already processed this hash
			if (db.HasHashInfoAndImport(_imageHash, importId)) {
				var _hashInfo = db.GetHashInfo(_imageHash);
				if (_hashInfo.paths == null) _hashInfo.paths = new HashSet<string>();
				_hashInfo.paths.Add(imagePath);
				db.AddOrUpdateHashInfo(_imageHash, progressId, _hashInfo, (int)ImportCode.IGNORED);
				return (int)ImportCode.IGNORED;
			}
			
			// checks if the hash has been imported before in another import
			var __hashInfo = db.GetHashInfo(_imageHash);
			if (__hashInfo != null) {
				if (__hashInfo.paths == null) __hashInfo.paths = new HashSet<string>();
				__hashInfo.paths.Add(imagePath);
				__hashInfo.imports.Add(importId);
				db.AddOrUpdateHashInfo(_imageHash, progressId, __hashInfo, (int)ImportCode.DUPLICATE);
				return (int)ImportCode.DUPLICATE;
			}
			
			if (IsImageCorrupt(imagePath)) {
				db.IncrementFailedCount(progressId, (int)ImportCode.FAILED);
				return (int)ImportCode.FAILED; 
			}

			string savePath = thumbnailPath + _imageHash.Substring(0,2) + "/" + _imageHash + ".thumb";
			(int _imageType, int _width, int _height) = GetImageInfo(imagePath);
			
			int _thumbnailType = (int)ImageType.ERROR;
			if (System.IO.File.Exists(savePath))
				_thumbnailType = GetActualFormat(savePath);
			else
				_thumbnailType = SaveThumbnail(imagePath, savePath, _imageHash, imageSize);

			if (_thumbnailType == (int)ImageType.ERROR) { 
				db.IncrementFailedCount(progressId, (int)ImportCode.FAILED);
				return (int)ImportCode.FAILED; 
			}
			ulong _diffHash = DifferenceHash(savePath);
			float[] _colorHash = ColorHash(savePath); 
			
			int _flags = 0;
			
			var hashInfo = new HashInfo {
				imageHash = _imageHash, 
				imageName = _imageName,
				differenceHash = _diffHash,
				colorHash = _colorHash,
				width = _width,
				height = _height,
				flags = _flags,
				thumbnailType = _thumbnailType,
				imageType = _imageType,
				size = imageSize,
				creationTime = imageCreationUtc,
				lastWriteTime = imageLastUpdateUtc,
				uploadTime = DateTime.Now.Ticks,
				lastEditTime = DateTime.Now.Ticks,
				paths = new HashSet<string>{imagePath},
				imports = new HashSet<string>{importId},
			};

			db.AddOrUpdateHashInfo(_imageHash, progressId, hashInfo, (int)ImportCode.SUCCESS);
			return (int)ImportCode.SUCCESS;	
		} 
		catch (Exception ex) { 
			GD.Print("ImageImporter::_ImportImage() : ", ex); 
			db.IncrementFailedCount(progressId, (int)ImportCode.FAILED);
			return (int)ImportCode.FAILED; 
		}
	}
	
}
