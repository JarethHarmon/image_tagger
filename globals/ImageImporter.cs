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
	public void PILFreeze(string[] _paths, string[] _savePaths, int maxSize)
	{
		try {
			string imagePaths="", savePaths="", saveTypes="";
			for (int i = 0; i < _paths.Length-1; i++) {
				imagePaths += _paths[i] + "?";
				savePaths += _savePaths[i] + "?";
				saveTypes += "jpeg" + "?";
			}
			imagePaths += _paths[_paths.Length-1];
			savePaths += _savePaths[_savePaths.Length-1];
			saveTypes += "jpeg";

			var pil = new Process();
			pil.StartInfo.FileName = @executableDirectory + @"lib/pil_thumbnail/pil_thumbnail.exe";
			pil.StartInfo.Arguments = String.Format("\"{0}\" \"{1}\" {2} {3}", imagePaths, savePaths, maxSize, saveTypes);
			pil.StartInfo.UseShellExecute = false;
			pil.StartInfo.CreateNoWindow = true;
			pil.StartInfo.RedirectStandardOutput = true;
			pil.StartInfo.RedirectStandardError = true;			

			pil.Start();
			var reader = pil.StandardOutput;
			string stderr = pil.StandardError.ReadToEnd();
			string output = String.Concat(reader.ReadToEnd().ToUpperInvariant().Where(c => !Char.IsWhiteSpace(c)));
			reader.Dispose();
			pil.Dispose();
		}
		catch (Exception ex) { GD.Print("FREEZE: ", ex); }
	}

	public void PILCompile(List<string> paths, List<string> savePaths, List<string> saveTypes, int maxSize)
	{
		string pyScript = System.IO.File.ReadAllText(@"R:\git\image_tagger\lib\python-3.10.7-embed-amd64\pil_thumbnail2.py");

		using (Py.GIL()) {
			try {
				var pyScope = Py.CreateScope();
				var pyCompiled = PythonEngine.Compile(pyScript);
				pyScope.Execute(pyCompiled);
				dynamic createThumbnails = pyScope.Get("save_thumbnails");
				dynamic result = createThumbnails(paths.ToArray(), savePaths.ToArray(), saveTypes.ToArray(), maxSize);
			}
			catch (PythonException pex) { GD.Print(pex.Message); }
			catch (Exception ex) { GD.Print(ex); }
		}
	}

	public void PILImport(List<string> paths, List<string> savePaths, List<string> saveTypes, int maxSize)
	{
		string pyScript = @"pil_thumbnail2";

		using (Py.GIL()) {
			try {
				dynamic test = Py.Import(pyScript);
				dynamic results = test.save_thumbnails(paths.ToArray(), savePaths.ToArray(), saveTypes.ToArray(), maxSize);
			}
			catch (PythonException pex) { GD.Print(pex.Message); }
			catch (Exception ex) { GD.Print(ex); }
		}
	}

	public void PILImportOne(List<string> paths, List<string> savePaths, List<string> saveTypes, int maxSize)
	{
		string pyScript = @"pil_thumbnail2"; // will need to ensure the script is in the correct location after building release

		for (int i = 0; i < paths.Count; i++) {
			
			using (Py.GIL()) {
				try {
					dynamic test = Py.Import(pyScript);
					// did not want to rewrite a temp function, these create new arrays with 1 element (the current index in paths/savePaths/saveTypes)
					dynamic results = test.save_thumbnails(new string[1]{paths.ToArray()[i]}, new string[1]{savePaths.ToArray()[i]}, new string[1]{saveTypes.ToArray()[i]}, maxSize);
				}
				catch (PythonException pex) { GD.Print(pex.Message); }
				catch (Exception ex) { GD.Print(ex); }
			}
		}
	}

	long timeFreeze=0, timeCompile=0, timeImport=0, timeImport1=0;
	public void CompareSpeed(List<string> paths)
	{
		string path1 = @"W:\シュヴィ\freeze\";
		string path2 = @"W:\シュヴィ\compile\";
		string path3 = @"W:\シュヴィ\import\";
		string path4 = @"W:\シュヴィ\importOne\";

		var savePaths1 = new List<string>();
		var savePaths2 = new List<string>();
		var savePaths3 = new List<string>();
		var savePaths4 = new List<string>();
		var saveTypes = new List<string>();
		foreach (string path in paths) {
			string _imageHash = (string) globals.Call("get_sha256", path);
			savePaths1.Add(path1 +  _imageHash + ".thumb");
			savePaths2.Add(path2 +  _imageHash + ".thumb");
			savePaths3.Add(path3 +  _imageHash + ".thumb");
			savePaths4.Add(path4 +  _imageHash + ".thumb");
			saveTypes.Add("jpeg");
		}

		long now = DateTime.Now.Ticks;
		PILFreeze(paths.ToArray(), savePaths1.ToArray(), 256);
		timeFreeze += DateTime.Now.Ticks-now;
		GD.Print("freeze: ", (float)timeFreeze/10000000);

		now = DateTime.Now.Ticks;
		PILCompile(paths, savePaths2, saveTypes, 256);
		timeCompile += DateTime.Now.Ticks-now;
		GD.Print("compile: ", (float)timeCompile/10000000);

		now = DateTime.Now.Ticks;
		PILImport(paths, savePaths3, saveTypes, 256);
		timeImport += DateTime.Now.Ticks-now;
		GD.Print("import: ", (float)timeImport/10000000);

		now = DateTime.Now.Ticks;
		PILImportOne(paths, savePaths4, saveTypes, 256);
		timeImport1 += DateTime.Now.Ticks-now;
		GD.Print("import1: ", (float)timeImport1/10000000);
		
		// results:
		//  freeze: 4.973003
		// compile: 5.014995
		//  import: 5.006996
		// import1: 4.840998
		// note that the real life time is less than this since they are running on multiple threads; counting the real time would require a major rewrite
	}

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
	
	private IntPtr state;
	public override void _Ready() 
	{
		globals = (Node) GetNode("/root/Globals");
		signals = (Node) GetNode("/root/Signals");
		iscan = (ImageScanner) GetNode("/root/ImageScanner");
		db = (Database) GetNode("/root/Database");
		string pyPath = @"R:\git\image_tagger\lib\python-3.10.7-embed-amd64\python310.dll";	// I believe this path is the only thing that will need to change for release build;
		// just calculate it dynamically using the exe path (release build test worked correctly)
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
		try {
			if (importId.Equals("") || progressId.Equals("")) return;
			string[] tabs = db.GetTabIDs(importId);
			string[] paths = db.GetPaths(progressId);
			if (paths == null) return;
			if (paths.Length == 0) return;
			int imageCount = db.GetTotalCount(importId);
			if (imageCount == 0) return;

			var pathList = new List<string>();
			int failCount = 0;
			for (int i = 0; i < paths.Length; i++) {
				if (NonExistent(paths[i])) failCount++;
				else pathList.Add(paths[i]);
			}
			_ImportImages(importId, progressId, pathList, tabs, imageCount, failCount);
		}
		catch (Exception ex) { GD.Print("ImageImporter::ImportImages() : ", ex); return; }
	}

	private HashSet<string> tempHashList = new HashSet<string>();
	private HashSet<string> blacklistSHA = new HashSet<string>();
	private static readonly object locker = new object();
	private void _ImportImages(string importId, string progressId, List<string> paths, string[] tabs, int imageCount, int failCount)
	{	
		CompareSpeed(paths);
		return;
		try {
			int[] results = new int[4]; 				// success,duplicate,ignored,failed
			results[(int)ImportCode.FAILED] += failCount;

			var hashInfoList = new List<HashInfo>();	// the images that did not fail (need to have metadata updated)
			var newHashInfoList = new List<HashInfo>();	// new images, need thumbnails and extra hashes
			var newHashInfoSavePaths = new List<string>();

			// iterate each path
			foreach (string path in paths) {
				var fileInfo = new System.IO.FileInfo(path);
				string _imageHash = (string) globals.Call("get_sha256", path);

				// for images that cause issues
				/*if (blacklistSHA.Contains(_imageHash)) {
					results[(int)ImportCode.IGNORED]++;
					continue;
				}*/

				// try to get HashInfo from tempStorage,Dictionary,Database
				var _hashInfo = db.GetHashInfo(importId, _imageHash);
				
				// if HashInfo was found in one of those locations
				if (_hashInfo != null) {
					_hashInfo.paths.Add(path);
					if (db.IsIgnored(importId, _imageHash) || IgnoredCheckerHas(importId, _imageHash)) 
						results[(int)ImportCode.IGNORED]++;
					else if (db.IsDuplicate(importId, _imageHash)) results[(int)ImportCode.DUPLICATE]++;
					hashInfoList.Add(_hashInfo);
					if (!ignoredChecker.ContainsKey(importId)) ignoredChecker[importId] = new HashSet<string>();
					ignoredChecker[importId].Add(_imageHash);
					continue;
				}
				if (!ignoredChecker.ContainsKey(importId)) ignoredChecker[importId] = new HashSet<string>();
				ignoredChecker[importId].Add(_imageHash);

				// else HashInfo was not found, check if it is corrupt
				if (IsImageCorrupt(path)) {
					results[(int)ImportCode.FAILED]++;
					continue;
				}

				// else generate the new HashInfo
				(int _imageType, int _width, int _height) = GetImageInfo(path);
				string savePath = thumbnailPath + _imageHash.Substring(0,2) + "/" + _imageHash + ".thumb";

				// add hashInfo to list for bulk processing
				var hashInfo = new HashInfo {
					paths = new HashSet<string>{path},
					imageName = (string) globals.Call("get_file_name", path),
					imageHash = _imageHash,
					size = fileInfo.Length,
					width = _width,
					height = _height,
					imageType = _imageType,
					creationTime = fileInfo.CreationTimeUtc.Ticks,
					lastWriteTime = fileInfo.LastWriteTimeUtc.Ticks,
					uploadTime = DateTime.Now.Ticks,
					lastEditTime = DateTime.Now.Ticks,
				};
				// only create the thumbnail if it does not already exist
				if (NonExistent(savePath)) {
					lock (locker) {
						if (!tempHashList.Contains(_imageHash)) {
							newHashInfoList.Add(hashInfo);
							newHashInfoSavePaths.Add(savePath);
						}
					}
				}
				db.StoreOneTempHashInfo(importId, progressId, hashInfo);
			}

			if (newHashInfoList.Count > 0) {
				// generate thumbnails using newHashInfoList and newHashInfoSavePaths
				(List<HashInfo> hashInfos, int[] results1) = SaveThumbnailsPIL(newHashInfoList, newHashInfoSavePaths.ToArray(), 256);
			
				// merge the array of results (jpg/png/fail) from thumbnail saving
				for (int i = 0; i < results.Length; i++) results[i] += results1[i];
			
				// iterate hashInfos and calc hashes
				//(might be done inside SaveThumbnailsPIL())

				foreach (HashInfo hashInfo in hashInfos) {
					hashInfoList.Add(hashInfo);
					tempHashList.Remove(hashInfo.imageHash);
				}
			}
			// call StoreTempHashInfo on hashInfoList
			db.StoreTempHashInfo(importId, progressId, hashInfoList, results);
			
			// update dictionaries
			db.UpdateImportCounts(importId, results); // update imports dictionary
			//globals.Call("_print", "::", results);
			foreach (HashInfo hashInfo in hashInfoList) {
				var __hashInfo = db.GetDictHashInfo(hashInfo.imageHash);
				if (__hashInfo != null) {
					db.MergeHashInfo(hashInfo, __hashInfo);
					db.UpsertDictHashInfo(hashInfo.imageHash, hashInfo);
				}
			}

			// call signals
			if (results[(int)ImportCode.SUCCESS] > 0 || results[(int)ImportCode.DUPLICATE] > 0) {
				if (tabs.Length > 0) 
					signals.Call("emit_signal", "increment_import_buttons", tabs, results[(int)ImportCode.SUCCESS] + results[(int)ImportCode.DUPLICATE]);
				signals.Call("emit_signal", "increment_all_button", results[(int)ImportCode.SUCCESS]);
			}
		}
		catch (Exception ex) { 
			GD.Print("ImageImporter::_ImportImages() : ", ex); 
		}
	}

	private (List<HashInfo>, int[]) SaveThumbnailsPIL(List<HashInfo> hashInfos, string[] _savePaths, int maxSize)
	{
		try {
			string imagePaths="", savePaths="", saveTypes="";
			for (int i = 0; i < hashInfos.Count-1; i++) {
				var _hashInfo = hashInfos[i];
				imagePaths += _hashInfo.paths.FirstOrDefault() + "?";
				savePaths += _savePaths[i] + "?";
				saveTypes += ((_hashInfo.size < AVG_THUMBNAIL_SIZE) ? "png" : "jpeg") + "?";
			}
			var hashInfo = hashInfos[hashInfos.Count-1];
			imagePaths += hashInfo.paths.FirstOrDefault();
			savePaths += _savePaths[hashInfos.Count-1];
			saveTypes += ((hashInfo.size < AVG_THUMBNAIL_SIZE) ? "png" : "jpeg");

			var pil = new Process();
			pil.StartInfo.FileName = @executableDirectory + @"lib/pil_thumbnail/pil_thumbnail.exe";
			pil.StartInfo.Arguments = String.Format("\"{0}\" \"{1}\" {2} {3}", imagePaths, savePaths, maxSize, saveTypes);
			pil.StartInfo.UseShellExecute = false;
			pil.StartInfo.CreateNoWindow = true;
			pil.StartInfo.RedirectStandardOutput = true;
			pil.StartInfo.RedirectStandardError = true;			

			pil.Start();
			var reader = pil.StandardOutput;
			string stderr = pil.StandardError.ReadToEnd();
			string output = String.Concat(reader.ReadToEnd().ToUpperInvariant().Where(c => !Char.IsWhiteSpace(c)));
			reader.Dispose();
			pil.Dispose();

			int[] intResults = new int[4]; // success,duplicate,ignored,failed
			string[] sep = new string[] { "?" };
			string[] results = output.Split(sep, StringSplitOptions.RemoveEmptyEntries);

			for (int i = 0; i < hashInfos.Count; i++)
			{
				if (results[i].Equals("JPEG")) {
					intResults[(int)ImportCode.SUCCESS]++;
					var tempHashInfo = hashInfos[i];
					var tempSavePath = _savePaths[i];
					tempHashInfo.thumbnailType = (int)ImageType.JPG;
					tempHashInfo.differenceHash = DifferenceHash(tempSavePath);
					tempHashInfo.colorHash = ColorHash(tempSavePath);
					tempHashInfo.perceptualHash = GetPerceptualHash(tempSavePath);
					hashInfos[i] = tempHashInfo;
				}
				else if (results[i].Equals("PNG")) {
					intResults[(int)ImportCode.SUCCESS]++;
					var tempHashInfo = hashInfos[i];
					var tempSavePath = _savePaths[i];
					tempHashInfo.thumbnailType = (int)ImageType.PNG;
					tempHashInfo.differenceHash = DifferenceHash(tempSavePath);
					tempHashInfo.colorHash = ColorHash(tempSavePath);
					tempHashInfo.perceptualHash = GetPerceptualHash(tempSavePath);
					hashInfos[i] = tempHashInfo;
				}
				else {
					GD.Print(results[i]);
					intResults[(int)ImportCode.FAILED]++;
					hashInfos.RemoveAt(i);
					// (prevent inserting broken image into database)
					//hashInfos[i].imageType = (int)ImageType.ERROR;
				}
			}

			return (hashInfos, intResults);
		}
		catch (Exception ex) { GD.Print("ImageImporter::SaveThumbnailsPIL() : ", ex); return (null, null); }
	}

	public Dictionary<string, HashSet<string>> ignoredChecker = new Dictionary<string, HashSet<string>>();
	private bool IgnoredCheckerHas(string importId, string imageHash)
	{
		if (!ignoredChecker.ContainsKey(importId)) return false;
		if (ignoredChecker[importId].Contains(imageHash)) return true;
		return false;
	}	

	public bool NonExistent(string path)
	{
		bool safePathLength = path.Length() < MAX_PATH_LENGTH;
		if ((safePathLength) ? !System.IO.File.Exists(path) : !Alphaleonis.Win32.Filesystem.File.Exists(path)) 
			return true;
		return false;
	}
}
