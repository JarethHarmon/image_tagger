using Godot;
using System;
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

public class ImportCodes
{
	public const int SUCCESS = 0;
	public const int DUPLICATE = 1;
	public const int IGNORED = 2;
	public const int FAILED = -1;
}

public class ImageCodes
{
	public const int JPG = 0;
	public const int PNG = 1;
	public const int FAIL = -1;
}

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
	
	public int SaveThumbnail(string savePath, string imageHash, long imageSize)
	{
		// in general need to remove invalid paths whenver they are iterated
		string[] imagePaths = db.GetPaths(imageHash); // import failing here, need to add paths to database first
		foreach (string path in imagePaths)
			if (FileExists(path))
				return _SaveThumbnail(path, savePath, imageSize);
		return ImageCodes.FAIL; // no paths exist
	}
	private int _SaveThumbnail(string imagePath, string thumbPath, long imageSize)
	{
		try {
			int result = ImageCodes.JPG; // 0 == JPG, 1 == PNG, -1 == ERR  (used to set HashInfo.thumbnailType in the Database) (need to create an Enum ideally)
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
				result = ImageCodes.PNG;
				im.Write(thumbPath);
				new ImageOptimizer().LosslessCompress(thumbPath);
			}
			return result;
		} catch (Exception ex) { GD.Print("ImageImporter::_SaveThumbnail() : ", ex); return ImageCodes.FAIL; }
	}
	public static string GetActualFormat(string imagePath)
	{
		try {
			if (imagePath.Length() < MAX_PATH_LENGTH) 
				return new MagickImageInfo(imagePath).Format.ToString().ToUpperInvariant().Replace("JPEG", "JPG");
			else
				return new MagickImageInfo(LoadFile(imagePath)).Format.ToString().ToUpperInvariant().Replace("JPEG", "JPG");
		} catch (Exception ex) { GD.Print("ImageImporter::GetActualFormat() : ", ex); return ""; }
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
	public int[] ColorHash(string path, int accuracy=1)
	{
	// made this up as I went, took a large number of iterations but it works pretty well
	// hash: ~4x faster than DifferenceHash, simi: ~55x slower than DifferenceHash (still ~0.6s/1M comparisons though)
	// higher accuracy numbers means lower accuracy and smaller hashes
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
	
/*=========================================================================================
									   Importing
=========================================================================================*/
	
	public void ImportImages(string importId, int imageCount)
	{
		int successCount = 0, duplicateCount = 0, ignoredCount = 0, failedCount = 0;
		var images = iscan.GetImages(); 
		// imagePath,imageType,imageCreationUtc,imageSize
		foreach ((string,string,long,long) imageInfo in images) {
			int result = _ImportImage(imageInfo, importId, imageCount);
			if (result == ImportCodes.SUCCESS) successCount++;
			else if (result == ImportCodes.DUPLICATE) duplicateCount++;
			else if (result == ImportCodes.IGNORED) ignoredCount++;
			else failedCount++;
			// update Import database (function call will also update dictionary (if user is viewing the page for this import))
			// update import_button
		}
		// checkpoint hash database
		// checkpoint import database
	}
	// need to check whether creating a MagickImage or getting a komi64/sha256 hash is faster
	// will need to reorder or remove IsImageCorrupt() call depending on results
	private int _ImportImage((string,string,long,long) imageInfo, string importId, int imageCount) 
	{
		try {
			(string imagePath, string imageType, long imageCreationUtc, long imageSize) = imageInfo;
			// check that the path/type/time/size meet the conditions specified by user (return ImportCodes.IGNORED if not)
		
			string imageHash = (string) globals.Call("get_sha256", imagePath); // get_komi_hash
			
			// I also need to add this importId to the hashes' HashSet of imports
			if (db.HashDatabaseContains(imageHash)) return ImportCodes.DUPLICATE;
			
			string savePath = thumbnailPath + imageHash + ".thumb";
			int result = SaveThumbnail(savePath, imageHash, imageSize);
			if (result == ImageCodes.FAIL) return ImportCodes.FAILED;
			string thumbnailType = "JPG";
			if (result == ImageCodes.PNG) thumbnailType = "PNG"; // should replace types with integers (no conversion + smaller storage)
			
			ulong diffHash = DifferenceHash(savePath);
			int[] colorHash = ColorHash(savePath, 4); // 4 = int[64] (1 = int[256])
			
			// include flags; will use default settings (for now will just pass 0 to signify no filter)
			int flags = 0;
			
			// database insert time will be calculated by the insert function, so no need to pass it as an argument
			// groups are irrelevant for initial insert (for now)
			// tags need to be passed as an argument from GDScript to ImportImages (and will be applied based on the users settings)
				// not going to worry about those for now, but eventually they will be a passed argument(s)
			// ratings will also be irrelevant for initial insert
			// this function will also add to the dictionary (if relevant (ie if the user is viewing the page for this import))
			//db.InsertHashInfo(imageHash, diffHash, colorHash, flags, thumbnailType, imageType, imageSize, imageCreationUtc, importId, imagePath);

			return ImportCodes.SUCCESS;	
		} catch (Exception ex) { GD.Print("ImageImporter::_ImportImage() : ", ex); return ImportCodes.FAILED; }
	}
	
}
