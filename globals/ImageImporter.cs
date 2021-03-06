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
			int result = (int)Data.ImageType.JPG; // 0 == JPG, 1 == PNG, -1 == ERR  (used to set HashInfo.thumbnailType in the Database) (need to create an Enum ideally)
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
				result = (int)Data.ImageType.PNG;
				im.Write(thumbPath);
				new ImageOptimizer().LosslessCompress(thumbPath);
			}
			return result;
		} catch (Exception ex) { GD.Print("ImageImporter::_SaveThumbnail() : ", ex); return (int)Data.ImageType.ERROR; }
	}
	
	public int GetActualFormat(string imagePath)
	{
		(string sformat, int width, int height) = _GetImageInfo(imagePath);
		int format = (int)Data.ImageType.OTHER;
		if ((bool) globals.Call("is_apng", imagePath)) format = (int)Data.ImageType.APNG;
		else if (sformat == "JPG") format = (int)Data.ImageType.JPG;
		else if (sformat == "PNG") format = (int)Data.ImageType.PNG;
		return format;
	}
	
	public (int, int, int) GetImageInfo(string imagePath)
	{
		(string sformat, int width, int height) = _GetImageInfo(imagePath);
		int format = (int)Data.ImageType.OTHER;
		if ((bool) globals.Call("is_apng", imagePath)) format = (int)Data.ImageType.APNG;
		else if (sformat == "JPG") format = (int)Data.ImageType.JPG;
		else if (sformat == "PNG") format = (int)Data.ImageType.PNG;
		
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
	public string GetRandomID(int num_bytes)
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
	public string CreateImportID()
	{
		return "I" + GetRandomID(8); // get 64bit ID
	}
	public string CreateTabID()
	{
		return "T" + GetRandomID(8);
	}	
	
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
	// replace ints with error codes
	public int ImportImage(string tabId)
	{
		string importId = db.GetImportId(tabId);
		if (importId.Equals("") || importId == null) return 1;
		int imageCount = db.GetTotalCount(importId);
		
		var image = iscan.GetImage(importId);
		if (image.Item1 == null || image.Item1.Equals("")) return 1;
	
		int result = _ImportImage(image, importId, imageCount);
		db.UpdateImportCount(importId, result);
		
		if (!db.ImportFinished(importId)) {
			signals.Call("emit_signal", "update_import_button", "All", true, db.GetSuccessCount("All"), db.GetTotalCount("All"), db.GetName("All"));
			signals.Call("emit_signal", "update_import_button", tabId, false, db.GetSuccessOrDuplicateCount(importId), imageCount, db.GetName(tabId));
		}
		return 0;	
	}
	
	public void FinishImport(string tabId)
	{	
		string importId = db.GetImportId(tabId);
		if (importId.Equals("")) return;
		
		db.FinishImport(importId);
		signals.Call("emit_signal", "update_import_button", "All", true, db.GetSuccessCount("All"), db.GetTotalCount("All"), db.GetName("All"));
		signals.Call("emit_signal", "update_import_button", tabId, true, db.GetSuccessOrDuplicateCount(importId), db.GetTotalCount(importId), db.GetName(tabId));
		db.CheckpointHashDB();
		db.CheckpointImportDB();
		Remove(importId);

	}
	
	public void AddToImportedHashes(string importId, string[] hashes)
	{
		lock (importedHashes) {
			importedHashes[importId] = new HashSet<string>(hashes);
		}
	}
	
	public string[] GetImportedHashes(string importId)
	{
		lock (importedHashes) {
			return (importedHashes.ContainsKey(importId)) ? importedHashes[importId].ToArray() : new string[0];
		}
	}
	
	// returns true if present already, returns false if added
	private bool CheckOrAdd(string importId, string imageHash)
	{
		lock(importedHashes) {
			if (importedHashes.ContainsKey(importId)) {
				if (importedHashes[importId].Contains(imageHash)) return true;
				importedHashes[importId].Add(imageHash);
				return false;
			}
			importedHashes[importId] = new HashSet<string>{imageHash};
			return false;
		}
	}
	
	private void Remove(string importId) 
	{
		lock(importedHashes) {
			importedHashes.Remove(importId);
		}
	}
	
	// need to check whether creating a MagickImage or getting a komi64/sha256 hash is faster  (hashing is much faster, even the slowest (GDnative sha512) is ~ 10x faster)
	private int _ImportImage((string,long,long) imageInfo, string importId, int imageCount) 
	{
		// I think duplicates should just add path/importid and load like successful images (incrementing duplicate count instead of success count though)
		try {
			(string imagePath, long imageCreationUtc, long imageSize) = imageInfo;
			// check that the path/type/time/size meet the conditions specified by user (return ImportCode.IGNORED if not)
			string imageHash = (string) globals.Call("get_sha256", imagePath); // get_komi_hash
			
			// checks if the current import has already processed this hash
			if (CheckOrAdd(importId, imageHash)) {
				db.AddPath(imageHash, imagePath);
				return (int)Data.ImportCode.IGNORED;
			}
			
			// checks if the current import has already processed this hash by checking if the hash 
			// is in the database AND the hashInfo imports list contains this importId (can probably replace above check with this)
			//	which means that the importedHashes array of saved data is not needed anymore
			if (db.HashDatabaseContainsImport(imageHash, importId)) {
				db.AddPath(imageHash, imagePath);
				return (int)Data.ImportCode.IGNORED;
			}
			
			// checks if the hash has been imported before in another import
			if (db.HashDatabaseContains(imageHash)) {
				db.AddImportId(imageHash, importId);
				db.AddPath(imageHash, imagePath);
				return (int)Data.ImportCode.DUPLICATE;
			}
			
			
			string savePath = thumbnailPath + imageHash.Substring(0,2) + "/" + imageHash + ".thumb";
			(int imageType, int width, int height) = GetImageInfo(imagePath);
			int thumbnailType = SaveThumbnail(imagePath, savePath, imageHash, imageSize);
			if (thumbnailType == (int)Data.ImageType.ERROR) return (int)Data.ImportCode.FAILED;
			
			ulong diffHash = DifferenceHash(savePath);
			float[] colorHash = ColorHash(savePath); // 4 = int[64] (1 = int[256])
			
			// include flags; will use default settings (for now will just pass 0 to signify no filter)
			int flags = 0;
			
			// database insert time will be calculated by the insert function, so no need to pass it as an argument
			// groups are irrelevant for initial insert (for now)
			// tags need to be passed as an argument from GDScript to ImportImages (and will be applied based on the users settings)
				// not going to worry about those for now, but eventually they will be a passed argument(s)
			// ratings will also be irrelevant for initial insert
			// this function will also add to the dictionary (if relevant (ie if the user is viewing the page for this import))
			db.InsertHashInfo(imageHash, diffHash, colorHash, flags, thumbnailType, imageType, imageSize, imageCreationUtc, importId, imagePath, width, height);
			
			return (int)Data.ImportCode.SUCCESS;	
		} catch (Exception ex) { GD.Print("ImageImporter::_ImportImage() : ", ex); return (int)Data.ImportCode.FAILED; }
	}
	
}
