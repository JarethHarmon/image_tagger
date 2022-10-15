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
using CoenM.ImageHash;
using CoenM.ImageHash.HashAlgorithms;
using ImageMagick;
using Data;
using Python.Runtime;	// pythonnet v3.0.0

namespace Importer {
public class PythonInterop : Node
{
	/*
		This class is used for inter-op with Python.
		1.	ImageImporter::LoadGif() calls pil_load_animation::get_gif_frames()
		2.	pil_load_animation imports this ('PythonInterop') class
		3.	pil_load_animation::get_gif_frames() instances this class and calls Setup()
		4.	said instance is now completely unrelated to Godot, but access to sceneTree is needed for signals
		5.	Setup() gains access to the mainloop > sceneTree > current instance of ImageImporter
		6.	now that there is a reference to Godot's instance of ImageImporter, python can pass the values back to it

		+ can now load frames 1-by-1 without re-opening and re-iterating the image for every frame
		+ this should result in similar load speeds, but with much lower initial frame delay
			(testing on a large Gif )
		- much harder to interrupt the load process
	*/
	public ImageImporter importer;
	public void Setup()
	{
		var mainLoop = Godot.Engine.GetMainLoop();
		var sceneTree = mainLoop as SceneTree;
		sceneTree.Root.AddChild(this);
		importer = (ImageImporter)GetNode("/root/ImageImporter");
	}

	private bool frameOne = true;
	public void SendFrameCount(dynamic d_frameCount)
	{
		int frameCount = (int)d_frameCount;
		importer.SendGifFrameCount(frameCount);
	}
	public void SendFrame(dynamic d_base64_str)
	{
		string base64_str = (string)d_base64_str;
		importer.SendGifFrame(base64_str);
	}
	public void SendAFrame(dynamic d_base64_str)
	{
		string base64_str = (string)d_base64_str;
		importer.SendAPngFrame(base64_str);
	}

	public void SetAnimatedImageType(dynamic d_isPng)
	{ 
		bool isPng = (bool)d_isPng;
		if (isPng) importer.setAnimatedImageType((int)ImageType.PNG);
		else importer.setAnimatedImageType((int)ImageType.JPG);
	}

	public bool StopLoading(dynamic d_path)
	{
		string imagePath = (string)d_path;
		if (importer.GetAnimationStatus(imagePath)) return true;
		return false;
	}
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
		try {
			string pyPath = @executableDirectory + @"lib\python-3.10.7-embed-amd64\python310.dll";
			//string pyPath = @"R:\git\image_tagger\bin\lib\python-3.10.7-embed-amd64\python310.dll"; // temp hardcoded path for release_debug
			System.Environment.SetEnvironmentVariable("PYTHONNET_PYDLL", pyPath);
			PythonEngine.Initialize();
			state = PythonEngine.BeginAllowThreads();
		}
		catch (PythonException pex) { GD.Print(pex.Message); }
		catch (Exception ex) { GD.Print(ex); }
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
		} 
		catch (Exception ex) { 
			GD.Print("ImageImporter::_GetImageInfo() : ", ex); 
			return ("", 0, 0); 
		}
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

	private int animatedImageType = (int)ImageType.JPG;
	public void setAnimatedImageType(int type) { animatedImageType = type; }
	public uint animatedFlags; // public so that it can be updated mid-load so that images do not have to be recreated with different flags as soon as they finish
	//private DateTime time;
	public void LoadGif(string imagePath, string imageHash)
	{
		//var label = (Label)GetNode("/root/main/Label");
		//time = DateTime.Now;
		string pyScript = @"pil_load_animation"; // "/lib/python-3.10.7-embed-amd64/pil_load_animation.py"
		int frameCount=0;
		using (Py.GIL()) {
			try {
				dynamic script = Py.Import(pyScript);
				script.get_gif_frames(@imagePath, imageHash);
			}
			catch (PythonException pex) { 
				GD.Print(pex.Message); 
				signals.Call("emit_signal", "finish_animation", imagePath);
				return;
			}
			catch (Exception ex) { 
				GD.Print(ex); 
				signals.Call("emit_signal", "finish_animation", imagePath);
				return;
			}
		}
		signals.Call("emit_signal", "finish_animation", imagePath);
		//label.Text += "\ngif_load   : " + (DateTime.Now-time).ToString();
	}

	private bool frameOne = true;
	public void SendGifFrameCount(int frameCount)
	{
		signals.Call("emit_signal", "set_animation_info", frameCount, 24);
		frameOne = true;
	}
	public void SendGifFrame(string base64_str)
	{
		//var label = (Label)GetNode("/root/main/Label");
		string[] sections = base64_str.Split('?');
		string imageHash = sections[0];
		string imagePath = sections[1];
		if (GetAnimationStatus(imagePath)) return;

		float delay;
		int temp;
		if (int.TryParse(sections[2], out temp)) delay = (float)temp/1000;
		else delay = (float)double.Parse(sections[2])/1000;

		string base64 = sections[3];
		byte[] bytes = System.Convert.FromBase64String(base64);
		var image = new Godot.Image();
		if (animatedImageType == (int)ImageType.JPG) image.LoadJpgFromBuffer(bytes);
		else image.LoadPngFromBuffer(bytes);

		var texture = new ImageTexture();
		texture.CreateFromImage(image, animatedFlags);
		texture.SetMeta("image_hash", imageHash);
		if (GetAnimationStatus(imagePath)) return;
		signals.Call("emit_signal", "add_animation_texture", texture, imagePath, delay, (frameOne) ? true : false);
		//if (frameOne)
		//	label.Text += "\nframe_one : " + (DateTime.Now-time).ToString();
		frameOne = false;
	}
	public void SendAPngFrame(string base64_str)
	{
		string[] sections = base64_str.Split('?');
		string imageHash = sections[0];
		string imagePath = sections[1];
		if (GetAnimationStatus(imagePath)) return;

		float delay;
		int temp;
		if (int.TryParse(sections[2], out temp)) delay = (float)temp/1000;
		else delay = (float)double.Parse(sections[2])/1000;

		string base64 = sections[3];
		byte[] bytes = System.Convert.FromBase64String(base64);
		var image = new Godot.Image();
		if (animatedImageType == (int)ImageType.JPG) image.LoadJpgFromBuffer(bytes);
		else image.LoadPngFromBuffer(bytes);

		var texture = new ImageTexture();
		texture.CreateFromImage(image, animatedFlags);
		texture.SetMeta("image_hash", imageHash);
		if (GetAnimationStatus(imagePath)) return;

		signals.Call("emit_signal", "add_animation_texture", texture, imagePath, delay, (frameOne) ? true : false);
		frameOne = false;
	}

	public void LoadAPng(string imagePath, string imageHash)
	{	
		string pyScript = @"pil_load_animation"; // "/lib/python-3.10.7-embed-amd64/pil_load_animation.py"
		int frameCount=0;
		using (Py.GIL()) {
			try {
				dynamic script = Py.Import(pyScript);
				script.get_apng_frames(@imagePath, imageHash);
			}
			catch (PythonException pex) { 
				GD.Print(pex.Message); 
				signals.Call("emit_signal", "finish_animation", imagePath);
				return;
			}
			catch (Exception ex) { 
				GD.Print(ex); 
				signals.Call("emit_signal", "finish_animation", imagePath);
				return;
			}
		}
		signals.Call("emit_signal", "finish_animation", imagePath);
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

	private long totalTimePythonReturn = 0, totalTimePythonSave = 0;
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

			/// TEMP CODE
			/*var now = DateTime.Now;
			TestReturn(path, thumbnailPath + fileHash.Substring(0,2) + "/" + fileHash + "ZZ.thumb", _saveType, thumbnailSize);
			totalTimePythonReturn += (DateTime.Now-now).Ticks;
			//GD.Print("python return: ", DateTime.Now-now);
			now = DateTime.Now;*/
			/// TEMP CODE

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

			/// TEMP CODE
			//GD.Print("python save: ", DateTime.Now-now);
			//totalTimePythonSave += (DateTime.Now-now).Ticks;
			//var label = (Label)GetNode("/root/main/Label");
			//label.Text = "python return: " + (totalTimePythonReturn/10000).ToString() + "\npython save: " + (totalTimePythonSave/10000).ToString();
			/// TEMP CODE

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
		string pyScript = @"pil_save_thumbnail";
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

	public void TestReturn(string imPath, string svPath, string svType, int svSize)
	{
		//var now = DateTime.Now;
		string pyScript = @"pil_save_thumbnail";
		string result = "";
		using (Py.GIL()) {
			try {
				dynamic script = Py.Import(pyScript);
				dynamic result_str = script.create_thumbnail(imPath, svType, svSize);
				result = (string)result_str;
			}
			catch (PythonException pex) { GD.Print(pex.Message); }
			catch (Exception ex) { GD.Print(ex); }
		}
		//GD.Print("python interop: ", DateTime.Now-now);
		//now = DateTime.Now;

		if (result.Equals("")) return;
		string[] parts = result.Split(new string[1]{"?"}, StringSplitOptions.None);
		if (parts.Length != 2) return;
		string s_thumbnailType = parts[0], thumbnailBase64 = parts[1];
		if (s_thumbnailType.Equals("-1")) return;
		if (thumbnailBase64.Equals("")) return;

		//GD.Print("safety checks: ", DateTime.Now-now);
		//now = DateTime.Now;

		int thumbnailType = -1;
		int.TryParse(s_thumbnailType, out thumbnailType);
		if (thumbnailType < 0) return;

		byte[] thumbnailData = System.Convert.FromBase64String(thumbnailBase64);
		var image = new Godot.Image();
		if (thumbnailType == (int)ImageType.JPG) image.LoadJpgFromBuffer(thumbnailData);
		else image.LoadPngFromBuffer(thumbnailData);
		var texture = new Godot.ImageTexture();
		texture.CreateFromImage(image, 0);

		//GD.Print("image creation: ", DateTime.Now-now);
		//now = DateTime.Now;

		var diffImage = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(thumbnailData);
		ulong diffHash = (new CoenM.ImageHash.HashAlgorithms.DifferenceHash()).Hash(diffImage);

		//GD.Print("diff hash: ", DateTime.Now-now);
		//now = DateTime.Now;

		//MemoryStream stream = new MemoryStream(thumbnailData);
		float[] colorHash = TestGetColorHash(diffImage);
		//globals.Call("_print", "colo", colorHash);

		//GD.Print("color hash: ", DateTime.Now-now);
		//now = DateTime.Now;

		var imm = new MagickImage(thumbnailData);
		string percHash = imm.PerceptualHash().ToString();

		//GD.Print("perc hash: ", DateTime.Now-now);
		//now = DateTime.Now;
		imm.Strip();
		imm.Write(svPath);
		// actual code will upload to the relevant thumbnail database here (which will likely be slower unfortunately)
		//		main goals are: improve speed of backups, improve speed of moving metadata, make it easier to share thumbnails, and
		//			load all thumbnails (for current page) from db at once (hopefully improving overall speed without taking too long to start showing thumbnails)
		//		main downsides are: will likely increase time before first thumbnails show up for current page, 
		//			will likely take up slightly more space, will likely take longer to save

		//GD.Print("saving: ", DateTime.Now-now);
		//now = DateTime.Now;
	}

	public float[] TestGetColorHash(SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> bitmap, int bucketSize=16) 
	{
		int[] colors = new int[256/bucketSize];
		int size = bitmap.Width * bitmap.Height;

		bitmap.ProcessPixelRows(accessor => 
		{
			for (int h = 0; h < accessor.Height; h++) {
				var row = accessor.GetRowSpan(h);
				for (int w = 0; w < row.Length; w++) {
					ref var pixel = ref row[w];
					int min_color = Math.Min(pixel.B, Math.Min(pixel.R, pixel.G));
					int max_color = Math.Max(pixel.B, Math.Max(pixel.R, pixel.G));
					int color1 = ((min_color/Math.Max(max_color, 1)) * (pixel.R+pixel.G+pixel.B) * pixel.A)/(766*bucketSize); 
					int color3 = (pixel.R+pixel.G+pixel.B)/(3*bucketSize);
					int color = (color1+color3)/2;
					colors[color]++;
				}
			}
		});

		float[] hash = new float[256/bucketSize];
		for (int color = 0; color < colors.Length; color++) {
			hash[color] = 100 * (float)colors[color]/size;
		}

		return hash;
	}
}
}