using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;	
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using Alphaleonis.Win32.Filesystem;
using AnimatedImages;
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
	
	public string executableDirectory;
	public void SetExecutableDirectory(string path) { executableDirectory = path; }

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
		//LoadUnsupportedImage(@"W:/test/17.jpg");
	}
	
/*=========================================================================================
										 IO
=========================================================================================*/
	public static byte[] LoadFile(string path)
	{
		try {
			if (!FileExists(path)) return new byte[0];
			return (path.Length() < MAX_PATH_LENGTH) ? System.IO.File.ReadAllBytes(path) : Alphaleonis.Win32.Filesystem.File.ReadAllBytes(path);
		}
		catch { return new byte[0]; }
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
	
	public int SaveThumbnail(string imagePath, string savePath, long imageSize)
	{
		// in general need to remove invalid paths whenver they are iterated
		return _SaveThumbnail(imagePath, savePath, imageSize);
	}
	private int _SaveThumbnail(string imagePath, string thumbPath, long imageSize)
	{
		try {
			int result = (int)ImageType.JPG; // 0 == JPG, 1 == PNG, -1 == ERR  (used to set HashInfo.thumbnailType in the Database)
			var im = (imagePath.Length() < MAX_PATH_LENGTH) ? new MagickImage(imagePath) : new MagickImage(LoadFile(imagePath));
			im.Strip();
			if (imageSize > AVG_THUMBNAIL_SIZE) {
				// need to test if resizing to 4,4 is actually relevant for monoColor images (especially if I also convert it to png)
				if (im.IsOpaque) im.Format = MagickFormat.Jpg;
				else {
					result = (int)ImageType.PNG;
					im.Format = MagickFormat.Png; // if image has transparency convert it to png
				}
				if (im.TotalColors == 1) im.Resize(4,4); // if mono-color image, save space
				else im.Resize(256, 256);
				im.Quality = 70; // was 50, increasing results in ~+20% file size & about ~+40% thumbnail quality
				im.Write(thumbPath);
				new ImageOptimizer().Compress(thumbPath);
			} else {
				im.Format = MagickFormat.Png;
				result = (int)ImageType.PNG;
				im.Write(thumbPath);
				new ImageOptimizer().LosslessCompress(thumbPath);
			}
			return result;
		} catch (Exception ex) { GD.Print("ImageImporter::_SaveThumbnail() : ", ex); return (int)ImageType.ERROR; }
	}
	
	public int GetNumColors(string imagePath)
	{
		try {
			var im = (imagePath.Length() < MAX_PATH_LENGTH) ? new MagickImage(imagePath) : new MagickImage(LoadFile(imagePath));
			return im.TotalColors;
		}
		catch (Exception ex) { GD.Print("ImageImporter::GetNumColors() : ", ex); return 0; }
	}

	// create a version of this that can call is_apng with a string of bytes
	// should take into account path length
	public int GetActualFormat(string imagePath)
	{
		(string sformat, int width, int height) = _GetImageInfo(imagePath);
		int format = (int)ImageType.OTHER;
		if ((bool) globals.Call("is_apng", imagePath)) format = (int)ImageType.APNG;
		else if (sformat == "JPG") format = (int)ImageType.JPG;
		else if (sformat == "PNG") format = (int)ImageType.PNG;
		else if (sformat == "GIF") format = (int)ImageType.GIF;
		return format;
	}
	
	public (int, int, int) GetImageInfo(string imagePath)
	{
		(string sformat, int width, int height) = _GetImageInfo(imagePath);
		int format = (int)ImageType.OTHER;
		if ((bool) globals.Call("is_apng", imagePath)) format = (int)ImageType.APNG;
		else if (sformat == "JPG") format = (int)ImageType.JPG;
		else if (sformat == "PNG") format = (int)ImageType.PNG;
		else if (sformat == "GIF") format = (int)ImageType.GIF;

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
	private static Godot.Image _LoadUnsupportedImage(MagickImage magickImage, long imageSize)
	{
		try {
			if (imageSize > AVG_THUMBNAIL_SIZE) {
				magickImage.Format = MagickFormat.Jpg;
				magickImage.Quality = 95;
				byte[] data = magickImage.ToByteArray();
				var image = new Godot.Image();
				image.LoadJpgFromBuffer(data);
				return image;
			} else {
				magickImage.Format = MagickFormat.Png;
				magickImage.Quality = 95;
				byte[] data = magickImage.ToByteArray();
				var image = new Godot.Image();
				image.LoadPngFromBuffer(data);
				return image;
			}
		} catch (Exception ex) { GD.Print("ImageImporter::_LoadUnsupportedImage() : ", ex); return null; }		
	}
	public static Godot.Image LoadUnsupportedImage(string imagePath)
	{
		try {
			byte[] imageData = LoadFile(imagePath);
			if (imageData.Length == 0) return null;	
			return _LoadUnsupportedImage(new MagickImage(imageData), imageData.Length);
		}
		catch (Exception ex) {
			GD.Print("ImageImporter::LoadUnsupportedImage() : ", ex);
			return null;
		}
	}

	public Dictionary<string, int> animationStatus = new Dictionary<string, int>();
	public void AddOrUpdateAnimationStatus(string path, int status) 
	{
		lock (animationStatus)
			animationStatus[path] = status;
	}
	public bool GetAnimationStatus(string path)
	{
		lock (animationStatus) {
			if (animationStatus[path] == (int)AnimationStatus.STOPPING) return true;
			return false;
		}
	}

	// this is code from my last attempt; currently uncertain if there is a better way
	// the basic concept behind this is :
	//	1. Godot AnimatedTexture has a frame limit of 256
	//	2. I want to return each frame as it loads (rather than doing nothing for potentially minutes)
	//	3. So instead, append each frame to an array as soon as it loads, code elsewhere will control iterating the array
	public uint animatedFlags; // public so that it can be updated mid-load so that images do not have to be recreated with different flags as soon as they finish
	public void LoadGif(string imagePath)
	{
		try {
			var frames = new MagickImageCollection(imagePath);
			int frameCount = frames.Count;
			if (frameCount <= 0) {
				signals.Call("emit_signal", "finish_animation", imagePath);
				return;
			}
			GD.Print("frames: ", frameCount);
			bool firstFrame = true; // need to consider changing logic to create a magickImage on the object, return it as the first frame, and then skip frame 1 in the foreach loop

			signals.Call("emit_signal", "set_animation_info", frameCount, 12); // need to actually calculate framerate
			frames.Coalesce();
			foreach (MagickImage frame in frames) {
				if (GetAnimationStatus(imagePath)) break;
				frame.Format = MagickFormat.Jpg;
				frame.Quality = 95;
				byte[] data = frame.ToByteArray();
				var image = new Godot.Image();
				image.LoadJpgFromBuffer(data);
				var texture = new ImageTexture();
				texture.CreateFromImage(image, animatedFlags);
				signals.Call("emit_signal", "add_animation_texture", texture, imagePath, 0f, (firstFrame) ? true : false); // need to actually calculate delay
				firstFrame = false;
			}
			frames.Clear();
			frames = null;
			signals.Call("emit_signal", "finish_animation", imagePath);
		}
		catch (Exception ex) {
			GD.Print("ImageImporter::LoadGif() : ", ex);
			signals.Call("emit_signal", "finish_animation", imagePath);
			return;
		}
	}

	public void LoadAPng(string imagePath)
	{	
		try {
			var apng = APNG.FromFile(imagePath);
			var frames = apng.Frames;
			int frameCount = apng.FrameCount;
			if (frameCount <= 0) {
				signals.Call("emit_signal", "finish_animation", imagePath);
				return;
			}
			GD.Print("frames: ", frameCount);
			bool firstFrame = true; // need to consider changing logic to create a magickImage on the object, return it as the first frame, and then skip frame 1 in the foreach loop

			signals.Call("emit_signal", "set_animation_info", frameCount, 12); // need to actually calculate framerate

			Bitmap prevFrame = null;
			for (int i = 0; i < frameCount; i++) {
				if (GetAnimationStatus(imagePath)) break;
				var image = new Godot.Image();
				var info = frames[i].GetAPngInfo();

				var newFrame = (i == 0) ? apng.DefaultImage.ToBitmap() : prevFrame;
				var graphics = Graphics.FromImage(newFrame);
				graphics.CompositingMode = CompositingMode.SourceOver;
				graphics.DrawImage(frames[i].ToBitmap(), info.xOffset, info.yOffset);
				prevFrame = newFrame;

				var converter = new ImageConverter();
				byte[] data = (byte[]) converter.ConvertTo(newFrame, typeof(byte[]));

				image.LoadPngFromBuffer(data);
				Array.Clear(data, 0, data.Length);
				var texture = new ImageTexture();
				texture.CreateFromImage(image, animatedFlags);
				signals.Call("emit_signal", "add_animation_texture", texture, imagePath, 0f, (firstFrame) ? true : false); // need to actually calculate delay
				firstFrame = false;
			}
			apng.ClearFrames();
			Array.Clear(frames, 0, frames.Length);
			if (prevFrame != null) {
				prevFrame.Dispose();
				prevFrame = null;
			}
			apng = null;
			frames = null;
			signals.Call("emit_signal", "finish_animation", imagePath);
		}
		catch (Exception ex) {
			GD.Print("ImageImporter::LoadAPng() : ", ex);
			signals.Call("emit_signal", "finish_animation", imagePath);
			return;
		}
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
		} catch (Exception ex) { GD.Print("Database::DifferenceHash() : ", ex); return 0; }
	}

	public string GetPerceptualHash(string path)
	{
		try {
			var im = (path.Length() < MAX_PATH_LENGTH) ? new MagickImage(path) : new MagickImage(LoadFile(path));
			return im.PerceptualHash().ToString();
		}
		catch (Exception ex) { GD.Print("Database::GetPerceptualHash() : ", ex); return ""; }
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
			//var file = ((path.Length() < MAX_PATH_LENGTH) ?
			//	new System.IO.FileInfo(path) : 
			//	new Alphaleonis.Win32.Filesystem.FileInfo(path));
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

	private int SaveThumbnailPIL(string imagePath, string savePath, int maxSize, long imageSize)
	{
		try {
			int result = (int)ImageType.ERROR;
			string saveType = "jpeg";
			if (imageSize < AVG_THUMBNAIL_SIZE)	saveType = "png";
			
			var pil = new Process();
			// need a way to get program location after launching (OS.get_executable_path() maybe)
			pil.StartInfo.FileName = @executableDirectory + @"lib/pil_thumbnail/pil_thumbnail.exe";
			pil.StartInfo.CreateNoWindow = true;
			pil.StartInfo.Arguments = String.Format("\"{0}\" \"{1}\" {2} {3}", imagePath, savePath, maxSize, saveType);
			pil.StartInfo.RedirectStandardOutput = true;
			pil.StartInfo.UseShellExecute = false;
			pil.Start();
			var reader = pil.StandardOutput;
			string output = String.Concat(reader.ReadToEnd().ToUpperInvariant().Where(c => !Char.IsWhiteSpace(c)));
			reader.Dispose();
			pil.Dispose();
			
			if (output.Equals("JPEG")) result = (int)ImageType.JPG;
			else if (output.Equals("PNG")) result = (int)ImageType.PNG;
			return result;
		}
		catch (Exception ex) { GD.Print("ImageImporter::SaveThumbnailPIL() : ", ex); return (int)ImageType.ERROR; }
	}

	public Dictionary<string, HashSet<string>> ignoredChecker = new Dictionary<string, HashSet<string>>();
	private bool IgnoredCheckerHas(string importId, string imageHash)
	{
		if (!ignoredChecker.ContainsKey(importId)) return false;
		if (ignoredChecker[importId].Contains(imageHash)) return true;
		return false;
	}

	private int _ImportImage((string,long,long,long) imageInfo, string importId, string progressId, int imageCount) 
	{
		try {
			(string imagePath, long imageSize, long imageCreationUtc, long imageLastUpdateUtc) = imageInfo;
			bool safePathLength = imagePath.Length() < MAX_PATH_LENGTH;
			if ((safePathLength) ? !System.IO.File.Exists(imagePath) : !Alphaleonis.Win32.Filesystem.File.Exists(imagePath)) {
				db.IncrementFailedCount(progressId, (int)ImportCode.FAILED);
				return (int)ImportCode.FAILED; 
			}

			// check that the path/type/time/size meet the conditions specified by user (return ImportCode.IGNORED if not)

			string _imageHash = (string) globals.Call("get_sha256", imagePath); 
			string _imageName = (string) globals.Call("get_file_name", imagePath);
			
			if (db.HasHashInfoAndImport(_imageHash, importId) || IgnoredCheckerHas(importId, _imageHash)) {
				var _hashInfo = db.GetHashInfo(_imageHash);
				if (_hashInfo.paths == null) _hashInfo.paths = new HashSet<string>();
				_hashInfo.paths.Add(imagePath);
				db.AddOrUpdateHashInfo(_imageHash, progressId, _hashInfo, (int)ImportCode.IGNORED);
				return (int)ImportCode.IGNORED;
			}
			else {
				if (!ignoredChecker.ContainsKey(importId)) ignoredChecker[importId] = new HashSet<string>();
				ignoredChecker[importId].Add(_imageHash);
			}

			// checks if the hash has been imported before in another import
			var __hashInfo = db.GetHashInfo(_imageHash);
			if (__hashInfo != null) {
				if (__hashInfo.paths == null) __hashInfo.paths = new HashSet<string>();
				__hashInfo.paths.Add(imagePath);
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
				_thumbnailType = SaveThumbnailPIL(imagePath, savePath, 256, imageSize); //SaveThumbnail(imagePath, savePath, imageSize);

			if (_thumbnailType == (int)ImageType.ERROR) { 
				db.IncrementFailedCount(progressId, (int)ImportCode.FAILED);
				return (int)ImportCode.FAILED; 
			}

			ulong _diffHash = DifferenceHash(savePath);
			float[] _colorHash = ColorHash(savePath);
			string _percHash = GetPerceptualHash(savePath);

			int _flags = 0;
			
			var hashInfo = new HashInfo {
				imageHash = _imageHash, 
				imageName = _imageName,
				differenceHash = _diffHash,
				colorHash = _colorHash,
				perceptualHash = _percHash,
				//numColors = GetNumColors(imagePath),
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
