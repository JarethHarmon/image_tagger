using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
	
	[SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "Reviewed. Suppression is OK here.")]
	[SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1001:CommasMustBeSpacedCorrectly", Justification = "Reviewed. Suppression is OK here.")]
	private static readonly byte[] _bitCounts =
	{
		0,1,1,2,1,2,2,3, 1,2,2,3,2,3,3,4, 1,2,2,3,2,3,3,4, 2,3,3,4,3,4,4,5,
		1,2,2,3,2,3,3,4, 2,3,3,4,3,4,4,5, 2,3,3,4,3,4,4,5, 3,4,4,5,4,5,5,6,
		1,2,2,3,2,3,3,4, 2,3,3,4,3,4,4,5, 2,3,3,4,3,4,4,5, 3,4,4,5,4,5,5,6,
		2,3,3,4,3,4,4,5, 3,4,4,5,4,5,5,6, 3,4,4,5,4,5,5,6, 4,5,5,6,5,6,6,7,
		1,2,2,3,2,3,3,4, 2,3,3,4,3,4,4,5, 2,3,3,4,3,4,4,5, 3,4,4,5,4,5,5,6,
		2,3,3,4,3,4,4,5, 3,4,4,5,4,5,5,6, 3,4,4,5,4,5,5,6, 4,5,5,6,5,6,6,7,
		2,3,3,4,3,4,4,5, 3,4,4,5,4,5,5,6, 3,4,4,5,4,5,5,6, 4,5,5,6,5,6,6,7,
		3,4,4,5,4,5,5,6, 4,5,5,6,5,6,6,7, 4,5,5,6,5,6,6,7, 5,6,6,7,6,7,7,8,
	};

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
	
	/*public int GetNumColors(string imagePath)
	{
		try {
			var im = (imagePath.Length() < MAX_PATH_LENGTH) ? new MagickImage(imagePath) : new MagickImage(LoadFile(imagePath));
			return im.TotalColors;
		}
		catch (Exception ex) { GD.Print("ImageImporter::GetNumColors() : ", ex); return 0; }
	}*/
	
	public (int, int, int) GetImageInfo(string imagePath)
	{
		(string sformat, int width, int height) = _GetImageInfo(imagePath);
		int format = (int)ImageType.OTHER;
		if ((bool) globals.Call("is_apng", imagePath)) format = (int)ImageType.APNG;
		else if (sformat == "JPG") format = (int)ImageType.JPG;
		else if (sformat == "PNG") format = (int)ImageType.PNG;
		else if (sformat == "GIF") format = (int)ImageType.GIF;
		else if (sformat == "WEBP") format = (int)ImageType.WEBP;

		return (format, width, height);
	}
	private static (string, int, int) _GetImageInfo(string imagePath)
	{
		try {
			var info = (imagePath.Length() < MAX_PATH_LENGTH) ? new MagickImageInfo(imagePath) : new MagickImageInfo(LoadFile(imagePath));
			string format = info.Format.ToString().ToUpperInvariant().Replace("JPEG", "JPG").Replace("JFIF", "JPG");
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
			// this is to prevent issues where the problem that caused Godot to fail to load the image
			//	persists through the creation of an ImageMagick image; basically just convert it to another format
			//	so that only the pixel data is carried over, with the header information being newly created
			// 	(might be a more efficient way to recreate extra data; need to check docs)
			if (magickImage.Format == MagickFormat.Jpeg || magickImage.Format == MagickFormat.Jpg) 
				magickImage.Format = MagickFormat.Png;
			else {
				magickImage.Format = MagickFormat.Jpeg;
				magickImage.Quality = 95;
			}
			byte[] data = magickImage.ToByteArray();
			var image = new Godot.Image();
			image.LoadPngFromBuffer(data);
			return image;
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
				importInfo.processed += failCount;
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

			int _imageType=(int)ImageType.ERROR, _thumbnailError=(int)ImageType.ERROR, _width=0, _height=0, result=(int)ImportCode.FAILED;
			long _size=fileInfo.Length;
			string savePath = thumbnailPath + fileHash.Substring(0,2) + "/" + fileHash + ".thumb";
			string _saveType = ((_size < AVG_THUMBNAIL_SIZE) ? "png" : "jpeg");

			// check if thumbnail exists; create it if not
			byte[] data = Array.Empty<byte>();
			if (FileDoesNotExist(savePath)) {
				(_imageType, _width, _height) = GetImageInfo(path);
				//(_thumbnailError, data) = SaveThumbnailWebp(path, savePath, _saveType, thumbnailSize);
				//data = SaveThumbnailWebp(path, savePath, thumbnailSize);
				_thumbnailError = SaveThumbnailWebp(path, savePath, thumbnailSize);
				// need to test if faster to return base64 string from python, or just read from disk
				// also need to check if they have fixed the issue causing byte[] returns from python to be extremely slow
				//	if so, would be better to avoid the pointless base64 conversions
				data = LoadFile(savePath);
			}

			var _hashInfo = db.GetHashInfo(importId, fileHash);
			if (_hashInfo == null) {
				// for when the thumbnail already exists but the metadata doesn't
				int h=0, w=0;
				if (_imageType == (int)ImageType.ERROR) (_imageType, _width, _height) = GetImageInfo(path);
				if (_thumbnailError == (int)ImageType.ERROR) (_thumbnailError, w, h) = GetImageInfo(savePath);
				if (data.Length == 0) data = LoadFile(savePath);

				var diffImage = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(data);
				ulong diffHash = (new CoenM.ImageHash.HashAlgorithms.DifferenceHash()).Hash(diffImage);

				int count = 0;
				ulong temp = diffHash;
				for (; temp > 0; temp >>= 8)
					count += _bitCounts[temp & 0xFF];
				

				float[] coloHash = CalcColorHash(diffImage);
				diffImage.Dispose();
				var imm = new MagickImage(data);
				string percHash = imm.PerceptualHash().ToString();

				_hashInfo = new HashInfo {
					imageHash = fileHash,
					imageName = (string)globals.Call("get_file_name", path),

					bucket1 = count,

					differenceHash = diffHash,
					colorHash = coloHash,
					perceptualHash = percHash,

					width = _width,
					height = _height,
					flags = 0,
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
				if (_hashInfo.differenceHash == 0) {
					var diffImage = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(data);
					ulong diffHash = (new CoenM.ImageHash.HashAlgorithms.DifferenceHash()).Hash(diffImage);
					diffImage.Dispose();
					_hashInfo.differenceHash = diffHash;
				}
				if (_hashInfo.colorHash == null) {
					var diffImage = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(data);
					float[] coloHash = CalcColorHash(diffImage);
					diffImage.Dispose();
					_hashInfo.colorHash = coloHash;
				}
				if (_hashInfo.perceptualHash == null) {
					var imm = new MagickImage(data);
					string percHash = imm.PerceptualHash().ToString();
					_hashInfo.perceptualHash = percHash;
				}
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

	//private byte[] SaveThumbnailWebp(string imPath, string svPath, int svSize)
	private int SaveThumbnailWebp(string imPath, string svPath, int svSize)
	{
		string pyScript = @"pil_save_thumbnail"; // need to make all of these into (const?static?readonly?) whichever avoids heap allocation
		//string results = String.Empty;
		int _result = -1;
		using (Py.GIL()) {
			try {
				dynamic script = Py.Import(pyScript);
				dynamic result = script.create_webp(imPath, svPath, svSize);
				//results = (string)result;
				_result = (int)result;
			}
			catch (PythonException pex) { GD.Print(pex.Message); }
			catch (Exception ex) { GD.Print(ex); }
		}
		return _result;
		/*if (results.Equals(String.Empty)) return Array.Empty<byte>();
		string[] parts = results.Split(new string[1]{"?"}, StringSplitOptions.None);
		if (parts.Length != 2) return Array.Empty<byte>();

		string thumbType = parts[0], base64Other = parts[1];
		if (thumbType.Equals("-1")) return Array.Empty<byte>();
		if (base64Other.Equals(String.Empty)) return Array.Empty<byte>();

		int thumbnailError = -1;
		int.TryParse(thumbType, out thumbnailError);
		if (thumbnailError < 0) return Array.Empty<byte>();
		byte[] webpData = System.Convert.FromBase64String(base64Other);

		return webpData;*/
	}

	public float[] CalcColorHash(SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> bitmap, int bucketSize=16) 
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