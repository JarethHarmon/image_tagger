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
using Python.Runtime;	// pythonnet v3.0.0-rc5

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
	}

	// called by Globals.gd/_ready()
	private IntPtr state;
	public void StartPython()
	{
		string pyPath = @executableDirectory + @"lib\python-3.10.7-embed-amd64\python310.dll";
		System.Environment.SetEnvironmentVariable("PYTHONNET_PYDLL", pyPath);
		PythonEngine.Initialize();
		state = PythonEngine.BeginAllowThreads();
	}

	public void Shutdown()
	{
		PythonEngine.EndAllowThreads(state);
		PythonEngine.Shutdown();
	}
	
/*=========================================================================================
									    Thumbnails
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
	public ulong GetDifferenceHash(string path)
	{
		try {
			var stream = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(@path);
			var algo = new CoenM.ImageHash.HashAlgorithms.DifferenceHash(); // PerceptualHash, DifferenceHash, AverageHash
			ulong result = algo.Hash(stream);
			stream.Dispose();
			return result;
		} catch (Exception ex) { 
			GD.Print("Database::DifferenceHash() : ", ex); 
			var label = (Label)GetNode("/root/main/Label");
			label.Text = ex.ToString();
			return 777; 
		}
	}

	public string GetPerceptualHash(string path)
	{
		try {
			var im = (path.Length() < MAX_PATH_LENGTH) ? new MagickImage(path) : new MagickImage(LoadFile(path));
			string result = im.PerceptualHash().ToString();
			im.Dispose();
			return result;
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
	
	public float[] GetColorHash(string path, int bucketSize=16) 
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
		bitmap.Dispose();

		float[] hash = new float[256/bucketSize];
		for (int color = 0; color < colors.Length; color++) {
			hash[color] = 100 * (float)colors[color]/size;
		}

		return hash;
	}
	
/*=========================================================================================
									   Importing
=========================================================================================*/
	public void ImportImages(string importId, string progressId)
	{
		if (importId.Equals("") || progressId.Equals("")) return;
		try {
			// verify there are paths to check
			string[] tabs = db.GetTabIDs(importId);
			string[] paths = db.GetPaths(progressId);
			if (paths == null) goto finish_section;
			if (paths.Length == 0) goto finish_section;

			// verify there are images to import
			int imageCount = db.GetTotalCount(importId);
			if (imageCount <= 0) goto finish_section;
			
			// count failed images and create list of working paths
			// filter paths here
			var pathList = new List<string>();
			int failCount = 0;
			for (int i = 0; i < paths.Length; i++) {
				if (FileDoesNotExist(paths[i])) {
					signals.Call("emit_signal", "increment_import_buttons", tabs); // indicate that an image has been processed
					failCount++;
				}
				else pathList.Add(paths[i]);
			}
			if (pathList.Count == 0) goto finish_section;

			// update dictImports with fail count
			if (failCount > 0) {
				var importInfo = db.GetImport(importId);
				importInfo.failed += failCount;
				db.AddImport(importId, importInfo);
			}

			// iterate each path and import it
			foreach (string path in pathList) {
				int result = _ImportImage(importId, progressId, path);
				signals.Call("emit_signal", "increment_import_buttons", tabs); // indicate that an image has been processed
				if (result == (int)ImportCode.SUCCESS) signals.Call("emit_signal", "increment_all_button");
				db.UpdateImportCount(importId, result);
			}

			finish_section:
				db.FinishImportSection(importId, progressId);
		}
		catch (Exception ex) {
			GD.Print("Importer::ImportImages(): ", ex);
			return;
		}
	}

	private bool FileDoesNotExist(string path) 
	{
		bool safePathLength = path.Length() < MAX_PATH_LENGTH;
		if ((safePathLength) ? System.IO.File.Exists(path) : Alphaleonis.Win32.Filesystem.File.Exists(path)) 
			return false;
		return true;
	}

	private int thumbnailSize = 256;
	private int _ImportImage(string importId, string progressId, string path)
	{
		try {
			var fileInfo = new System.IO.FileInfo(path);
			string fileHash = (string) globals.Call("get_sha256", path);
			// filter hashes here

			int _imageType=(int)ImageType.ERROR, _thumbnailType=(int)ImageType.ERROR, _width=0, _height=0, result=(int)ImportCode.FAILED;
			long _size=fileInfo.Length;
			string savePath = thumbnailPath + fileHash.Substring(0,2) + "/" + fileHash + ".thumb";
			string _saveType = ((_size < AVG_THUMBNAIL_SIZE) ? "png" : "jpeg");

			// check if thumbnail exists; create it if not
			if (FileDoesNotExist(savePath)) {
				(_imageType, _width, _height) = GetImageInfo(path);
				_thumbnailType = SaveThumbnailPIL(path, savePath, _saveType, thumbnailSize);
			}

			var _hashInfo = db.GetHashInfo(importId, fileHash);
			if (_hashInfo == null) {
				// for when the thumbnail already exists but the metadata doesn't
				int h=0, w=0;
				if (_imageType == (int)ImageType.ERROR) (_imageType, _width, _height) = GetImageInfo(path);
				if (_thumbnailType == (int)ImageType.ERROR) (_thumbnailType, w, h) = GetImageInfo(savePath);

				_hashInfo = new HashInfo {
					imageHash = fileHash,
					imageName = (string)globals.Call("get_file_name", path),

					differenceHash = GetDifferenceHash(savePath),
					colorHash = GetColorHash(savePath),
					perceptualHash = GetPerceptualHash(savePath),

					width = _width,
					height = _height,
					flags = 0,
					thumbnailType = _thumbnailType,
					imageType = _imageType,
					size = _size,
					creationTime = fileInfo.CreationTimeUtc.Ticks,
					lastWriteTime = fileInfo.LastWriteTimeUtc.Ticks,
					uploadTime = DateTime.Now.Ticks,
					lastEditTime = DateTime.Now.Ticks,

					isGroupLeader = false,
					imports = new HashSet<string>{importId},
					paths = new HashSet<string>{path},
				};
				result = (int)ImportCode.SUCCESS;
			}
			else {
				if (_hashInfo.differenceHash == 0) _hashInfo.differenceHash = GetDifferenceHash(savePath);
				if (_hashInfo.colorHash == null) _hashInfo.colorHash = GetColorHash(savePath);
				if (_hashInfo.perceptualHash == null) _hashInfo.perceptualHash = GetPerceptualHash(savePath);
				_hashInfo.paths.Add(path);

				if (_hashInfo.imports.Contains(importId)) result = (int)ImportCode.IGNORED;
				else {
					_hashInfo.imports.Add(importId);
					result = (int)ImportCode.DUPLICATE;
				}
			}

			db.StoreTempHashInfo(importId, progressId, _hashInfo);
			return result;
		}
		catch (Exception ex) { 
			GD.Print("Importer::_ImportImage() : ", ex);
			return (int)ImportCode.FAILED;
		}
	}

	private int SaveThumbnailPIL(string imPath, string svPath, string svType, int svSize)
	{
		string pyScript = @"pil_thumbnail3";
		int result = (int)ImageType.OTHER;
		using (Py.GIL()) {
			try {
				dynamic script = Py.Import(pyScript);
				dynamic image_type = script.save_thumbnail(imPath, svPath, svType, svSize);
				result = (int)image_type;
			}
			catch (PythonException pex) { GD.Print(pex.Message); }
			catch (Exception ex) { GD.Print(ex); }
		}
		return result;
	}
}
