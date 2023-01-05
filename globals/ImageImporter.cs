using Godot;
using System;
using ImageMagick;
using Data;
using Python.Runtime;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Importer
{
    // still want to test using static functions on main class instead of doing this
    public class PythonInterop : Node
    {
        public ImageImporter importer;
        public void Setup()
        {
            var mainLoop = Godot.Engine.GetMainLoop();
            var scenetree = mainLoop as SceneTree;
            scenetree.Root.AddChild(this);
            importer = GetNode<ImageImporter>("/root/ImageImporter");
        }

        public void SendFrameCount(dynamic d_frameCount)
        {
            int frameCount = (int)d_frameCount;
            importer.SendFrameCount(frameCount);
        }

        public void SendAnimationFrame(dynamic d_base64)
        {
            string base64 = (string)d_base64;
            importer.SendAnimationFrame(base64);
        }

        public bool StopLoading(dynamic d_hash)
        {
            string hash = (string)d_hash;
            return importer.StopLoading(hash);
        }

        public void SendImageTile(dynamic d_base64)
        {
            string base64 = (string)d_base64;
            importer.SendImageTile(base64);
        }
    }

    public sealed class ImageImporter : Node
    {
        public const int MAX_PATH_LENGTH = 256, THUMBNAIL_SIZE = 256;

        public Node globals, signals;
        public ImageScanner scanner;
        public Database db;

        public string thumbnailPath;
        public void SetThumbnailPath(string path) { thumbnailPath = path; }
        public string executableDirectory;
        public void SetExecutableDirectory(string path) { executableDirectory = path; }

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
            globals = GetNode<Node>("/root/Globals");
            signals = GetNode<Node>("/root/Signals");
            scanner = GetNode<ImageScanner>("/root/ImageScanner");
            db = GetNode<Database>("/root/Database");
        }

        private IntPtr state;
        public void StartPython()
        {
            try
            {
                string pyPath = @executableDirectory + @"lib\python-3.10.7-embed-amd64\python310.dll";
                //string pyPath = @executableDirectory + @"lib\python-3.11.1-embed-amd64\python311.dll"; // has issues with numpy currently
                System.Environment.SetEnvironmentVariable("PYTHONNET_PYDLL", pyPath);
                PythonEngine.Initialize();
                state = PythonEngine.BeginAllowThreads();
            }
            catch (PythonException pex) { GD.Print(pex); }
            catch (Exception ex) { GD.Print(ex); }
        }

        public void Shutdown()
        {
            PythonEngine.EndAllowThreads(state);
            PythonEngine.Shutdown();
        }

        /*=========================================================================================
									    Thumbnails & Perceptual Hashes
        =========================================================================================*/

        public struct PerceptualHashes
        {
            public ulong average;
            public ulong wavelet;
            public ulong difference;

            public PerceptualHashes(ulong average, ulong wavelet, ulong difference)
            {
                this.average = average;
                this.wavelet = wavelet;
                this.difference = difference;
            }
        }

        public struct ColorBuckets
        {
            public int red;
            public int green;
            public int blue;
            public int alpha;
            public int light;
            public int dark;

            public ColorBuckets(string _colors)
            {
                string[] temp = _colors.Split('?');
                if (temp?.Length == 6)
                {
                    int[] colors = new int[6];
                    for (int i = 0; i < temp.Length; i++)
                    {
                        if (!int.TryParse(temp[i], out colors[i])) colors[i] = 0;
                    }

                    this.red = colors[0];
                    this.green = colors[1];
                    this.blue = colors[2];
                    this.alpha = colors[3];
                    this.light = colors[4];
                    this.dark = colors[5];
                }
                else
                {
                    this.red = 0;
                    this.green = 0;
                    this.blue = 0;
                    this.alpha = 0;
                    this.light = 0;
                    this.dark = 0;
                }
            }
        }

        public (PerceptualHashes, ColorBuckets) SaveThumbnailAndGetPerceptualHashes(string imagePath, string savePath, int thumbSize)
        {
            const string pyScript = "pil_thumbnail_perceptual_hashes";
            using (Py.GIL())
            {
                try
                {
                    dynamic script = Py.Import(pyScript);
                    script.save_webp_thumbnail(imagePath, savePath, thumbSize);

                    dynamic _average = script.calc_average_hash();
                    ulong average = (ulong)_average;

                    dynamic _wavelet = script.calc_wavelet_hash();
                    ulong wavelet = (ulong)_wavelet;

                    dynamic _difference = script.calc_dhash();
                    ulong difference = (ulong)_difference;

                    dynamic _color_buckets = script.calc_color_buckets();
                    string colorBuckets = (string)_color_buckets;

                    return (new PerceptualHashes(average, wavelet, difference), new ColorBuckets(colorBuckets));
                }
                catch (PythonException pex)
                {
                    GD.Print("pex: ", pex);
                    return (new PerceptualHashes(), new ColorBuckets());
                }
                catch (Exception ex)
                {
                    GD.Print("ex: ", ex);
                    return (new PerceptualHashes(), new ColorBuckets());
                }
            }
        }

        public (PerceptualHashes, ColorBuckets) GetPerceptualHashes(string savePath)
        {
            const string pyScript = "pil_thumbnail_perceptual_hashes";
            using (Py.GIL())
            {
                try
                {
                    dynamic script = Py.Import(pyScript);
                    script.initialize(savePath);

                    dynamic _average = script.calc_average_hash();
                    ulong average = (ulong)_average;

                    dynamic _wavelet = script.calc_wavelet_hash();
                    ulong wavelet = (ulong)_wavelet;

                    dynamic _difference = script.calc_dhash();
                    ulong difference = (ulong)_difference;

                    dynamic _color_buckets = script.calc_color_buckets();
                    string colorBuckets = (string)_color_buckets;

                    return (new PerceptualHashes(average, wavelet, difference), new ColorBuckets(colorBuckets));
                }
                catch (PythonException pex)
                {
                    GD.Print("pex: ", pex);
                    return (new PerceptualHashes(), new ColorBuckets());
                }
                catch (Exception ex)
                {
                    GD.Print("ex: ", ex);
                    return (new PerceptualHashes(), new ColorBuckets());
                }
            }
        }

        /*=========================================================================================
									               IO
        =========================================================================================*/
        private static bool FileExists(string path)
        {
            return (path.Length < MAX_PATH_LENGTH)
                ? System.IO.File.Exists(path)
                : Alphaleonis.Win32.Filesystem.File.Exists(path);
        }

        private static bool TryLoadFile(string path, out byte[] data)
        {
            try
            {
                if (!FileExists(path))
                {
                    data = Array.Empty<byte>();
                    return false;
                }

                if (path.Length < MAX_PATH_LENGTH) data = System.IO.File.ReadAllBytes(path);
                else data = Alphaleonis.Win32.Filesystem.File.ReadAllBytes(path);
                return true;
            }
            catch
            {
                data = Array.Empty<byte>();
                return false;
            }
        }

        public static bool IsImageCorrupt(string path)
        {
            try
            {
                var image = (path.Length() < MAX_PATH_LENGTH)
                    ? new MagickImage(path)
                    : new MagickImage(TryLoadFile(path, out byte[] data) ? data : Array.Empty<byte>());
                return false;
            }
            catch
            {
                return true;
            }
        }

        private struct FileInfo
        {
            internal long Size;
            internal long CreationTime;
            internal long LastWriteTime;

            internal FileInfo(string path)
            {
                try
                {
                    if (path.Length() < MAX_PATH_LENGTH)
                    {
                        var info = new System.IO.FileInfo(path);
                        Size = info.Length;
                        CreationTime = info.CreationTimeUtc.Ticks;
                        LastWriteTime = info.LastWriteTimeUtc.Ticks;
                        info = null;
                    }
                    else
                    {
                        var info = new Alphaleonis.Win32.Filesystem.FileInfo(path);
                        Size = info.Length;
                        CreationTime = info.CreationTimeUtc.Ticks;
                        LastWriteTime = info.LastWriteTimeUtc.Ticks;
                        info = null;
                    }
                }
                catch
                {
                    Size = -1;
                    CreationTime = -1;
                    LastWriteTime = -1;
                }
            }
        }

        private struct ImageInfo
        {
            internal ImageType ImageType;
            internal int Width;
            internal int Height;

            internal ImageInfo(ImageType imageType, int width, int height)
            {
                ImageType = imageType;
                Width = width;
                Height = height;
            }

            internal ImageInfo(string path)
            {
                try
                {
                    var info = (path.Length() < MAX_PATH_LENGTH)
                        ? new MagickImageInfo(path)
                        : new MagickImageInfo(TryLoadFile(path, out byte[] data) ? data : Array.Empty<byte>());

                    Width = info.Width;
                    Height = info.Height;
                    ImageType = ImageType.OTHER;

                    string format = info.Format.ToString().ToUpperInvariant().Replace("JPG", "JPEG").Replace("JFIF", "JPEG");
                    if (format.Equals("jpg", StringComparison.InvariantCultureIgnoreCase)) ImageType = ImageType.JPG;
                    else if (format.Equals("png", StringComparison.InvariantCultureIgnoreCase)) ImageType = ImageType.PNG;
                    else if (format.Equals("gif", StringComparison.InvariantCultureIgnoreCase)) ImageType = ImageType.GIF;
                    else if (format.Equals("webp", StringComparison.InvariantCultureIgnoreCase)) ImageType = ImageType.WEBP;
                }
                catch
                {
                    ImageType = ImageType.ERROR;
                    Width = -1;
                    Height = -1;
                }
            }

            internal ImageInfo(string path, ImageType imageType)
            {
                try
                {
                    var info = (path.Length() < MAX_PATH_LENGTH)
                        ? new MagickImageInfo(path)
                        : new MagickImageInfo(TryLoadFile(path, out byte[] data) ? data : Array.Empty<byte>());

                    ImageType = imageType;
                    Width = info.Width;
                    Height = info.Height;
                }
                catch
                {
                    ImageType = ImageType.ERROR;
                    Width = -1;
                    Height = -1;
                }
            }
        }

        private ImageInfo GetImageInfo(string path)
        {
            return ((bool)globals.Call("is_apng", path))
                ? new ImageInfo(path, ImageType.APNG)
                : new ImageInfo(path);
        }

        public static Godot.Image LoadUnsupportedImage(string path)
        {
            try
            {
                if (TryLoadFile(path, out byte[] data))
                {
                    var image = new MagickImage(data);

                    // tries to resolve issue where image is saved as wrong type, jpeg/png swaps are by far the most common
                    if (image.Format == MagickFormat.Jpeg || image.Format == MagickFormat.Jpg)
                    {
                        image.Format = MagickFormat.Png;
                        byte[] newData = image.ToByteArray();
                        var newImage = new Godot.Image();
                        newImage.LoadPngFromBuffer(newData);
                        return newImage;
                    }
                    else
                    {
                        image.Format = MagickFormat.Jpeg;
                        image.Quality = 95;
                        byte[] newData = image.ToByteArray();
                        var newImage = new Godot.Image();
                        newImage.LoadJpgFromBuffer(newData);
                        return newImage;
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        /*=========================================================================================
									      Animations & Large Images
        =========================================================================================*/
        public bool StopLoading(string hash)
        {
            return db.IncorrectImage(hash);
        }

        public void LoadGif(string path, string hash)
        {
            const string pyScript = "pil_load_animation";
            using (Py.GIL())
            {
                try
                {
                    dynamic script = Py.Import(pyScript);
                    script.get_gif_frames(path, hash);
                }
                catch (PythonException pex)
                {
                    GD.Print(pex);
                    signals.Call("emit_signal", "finish_animation", hash);
                    return;
                }
                catch (Exception ex)
                {
                    GD.Print(ex);
                    signals.Call("emit_signal", "finish_animation", hash);
                    return;
                }
            }
            signals.Call("emit_signal", "finish_animation", hash);
        }

        public void LoadAPng(string path, string hash)
        {
            const string pyScript = "pil_load_animation";
            using (Py.GIL())
            {
                try
                {
                    dynamic script = Py.Import(pyScript);
                    script.get_apng_frames(path, hash);
                }
                catch (PythonException pex)
                {
                    GD.Print(pex);
                    signals.Call("emit_signal", "finish_animation", hash);
                    return;
                }
                catch (Exception ex)
                {
                    GD.Print(ex);
                    signals.Call("emit_signal", "finish_animation", hash);
                    return;
                }
            }
            signals.Call("emit_signal", "finish_animation", hash);
        }

        private bool frameOne = true;
        public void SendFrameCount(int count)
        {
            signals.Call("emit_signal", "set_animation_info", count, 24);
            frameOne = true;
        }

        private Godot.ImageTexture GetImageTexture(string data, string hash, string type)
        {
            byte[] bytes = System.Convert.FromBase64String(data);
            var image = new Godot.Image();
            if (type.Equals("jpeg", StringComparison.InvariantCultureIgnoreCase)) image.LoadJpgFromBuffer(bytes);
            else image.LoadPngFromBuffer(bytes);
            if (StopLoading(hash)) return null;

            var texture = new Godot.ImageTexture();
            texture.CreateFromImage(image, 0);
            texture.SetMeta("image_hash", hash);
            return texture;
        }

        public void SendAnimationFrame(string base64)
        {
            string[] sections = base64.Split('?');
            string type = sections[0], hash = sections[1], data = sections[3];
            if (StopLoading(hash)) return;

            float delay;
            if (int.TryParse(sections[2], out int temp)) delay = (float)temp / 1000;
            else delay = (float)double.Parse(sections[2]) / 1000;

            var texture = GetImageTexture(data, hash, type);
            signals.Call("emit_signal", "add_animation_texture", texture, hash, delay, frameOne);
            frameOne = false;
        }

        private int currentGridIndex = 0;
        public void SendImageTile(string base64)
        {
            string[] sections = base64.Split('?');
            string type = sections[0], hash = sections[1], data = sections[2];
            if (StopLoading(hash)) return;

            var texture = GetImageTexture(data, hash, type);
            signals.Call("emit_signal", "add_large_image_section", texture, hash, currentGridIndex);
            currentGridIndex++;
        }

        public void LoadLargeImage(string path, string hash, int columns, int rows)
        {
            const string pyScript = "pil_load_large_image";
            using (Py.GIL())
            {
                try
                {
                    currentGridIndex = 0;
                    dynamic script = Py.Import(pyScript);
                    script.load_large_image(path, hash, columns, rows);
                }
                catch (PythonException pex)
                {
                    GD.Print("pex: ", pex);
                    signals.Call("emit_signal", "finish_large_image", hash);
                    return;
                }
                catch (Exception ex)
                {
                    GD.Print("ex: ", ex);
                    signals.Call("emit_signal", "finish_large_image", hash);
                    return;
                }
            }
            signals.Call("emit_signal", "finish_large_image", hash);
        }

        /*=========================================================================================
									             Hashing
        =========================================================================================*/
        private string GetRandomId(int numBytes)
        {
            byte[] bytes = new byte[numBytes];
            var rng = new RNGCryptoServiceProvider();
            rng.GetBytes(bytes);
            rng.Dispose();
            return BitConverter.ToString(bytes).Replace("-", "");
        }

        public string CreateImportId() { return $"I{GetRandomId(8)}"; }
        public string CreateTabId() { return $"T{GetRandomId(8)}"; }
        public string CreateGroupId() { return $"G{GetRandomId(8)}"; }
        public string CreateSectionId() { return $"S{GetRandomId(8)}"; }

        /*=========================================================================================
									             Importing
        =========================================================================================*/
        public void ImportImages(string importId, string sectionId)
        {
            if (importId.Equals(string.Empty) || sectionId.Equals(string.Empty)) return;
            string[] tabs = db.GetTabIDs(importId);
            string[] paths = db.GetPaths(sectionId);
            //GD.Print(string.Join("\n", paths));

            var info = db.GetImport(importId);
            if (info is null || info.total <= 0)
            {
                db.DeleteImportSection(sectionId); // otherwise paths can get left in the database
                return;
            }

            var validPaths = new List<string>();
            foreach (string path in paths)
            {
                if (!FileExists(path))
                {
                    db.UpdateImportCount(importId, ImportCode.FAILED);
                }
                else
                {
                    validPaths.Add(path);
                }
            }

            //GD.Print(string.Join("\n", validPaths));

            foreach (string path in validPaths)
            {
                ImportCode result = ImportImage(importId, sectionId, path);
                if (result == ImportCode.SUCCESS)
                {
                    signals.Call("emit_signal", "increment_all_button");
                }
                db.UpdateImportCount(importId, result);
                signals.Call("emit_signal", "increment_import_buttons", tabs); // indicate that an image has been processed
            }

            db.FinishImportSection(importId, sectionId);
        }

        public ImportCode ImportImage(string importId, string sectionId, string imagePath)
        {
            var fileInfo = new FileInfo(imagePath);
            if (fileInfo.Size < 0) return ImportCode.FAILED;
            string hash = (string)globals.Call("get_sha256", imagePath);

            string thumbPath = $"{thumbnailPath}{hash.Substring(0, 2)}/{hash}.thumb";
            PerceptualHashes perceptualHashes = new PerceptualHashes();
            ColorBuckets colorBuckets = new ColorBuckets();
            ImportCode result = ImportCode.SUCCESS;

            bool thumbnailExisted = true;
            if (!FileExists(thumbPath))
            {
                thumbnailExisted = false;
                (perceptualHashes, colorBuckets) = SaveThumbnailAndGetPerceptualHashes(imagePath, thumbPath, THUMBNAIL_SIZE);
                if (perceptualHashes.difference == 0 || !FileExists(thumbPath)) return ImportCode.FAILED;
            }

            var hashInfo = db.GetHashInfo(importId, hash);
            if (hashInfo is null)
            {
                var imageInfo = GetImageInfo(imagePath);
                if (imageInfo.ImageType == ImageType.ERROR) return ImportCode.FAILED;

                if (thumbnailExisted)
                {
                    (perceptualHashes, colorBuckets) = GetPerceptualHashes(thumbPath);
                    if (perceptualHashes.difference == 0) return ImportCode.FAILED;
                }

                ulong temp = perceptualHashes.difference;
                int bucket = 0;
                for (; temp > 0; temp >>= 8)
                    bucket += _bitCounts[temp & 0xff];

                hashInfo = new HashInfo
                {
                    imageHash = hash,
                    imageName = (string)globals.Call("get_file_name", imagePath),

                    averageHash = perceptualHashes.average,
                    waveletHash = perceptualHashes.wavelet,
                    differenceHash = perceptualHashes.difference,

                    red = colorBuckets.red,
                    green = colorBuckets.green,
                    blue = colorBuckets.blue,
                    alpha = colorBuckets.alpha,
                    light = colorBuckets.light,
                    dark = colorBuckets.dark,

                    width = imageInfo.Width,
                    height = imageInfo.Height,
                    imageType = (int)imageInfo.ImageType,
                    size = fileInfo.Size,

                    creationTime = fileInfo.CreationTime,
                    lastWriteTime = fileInfo.LastWriteTime,
                    lastEditTime = DateTime.UtcNow.Ticks,
                    uploadTime = DateTime.UtcNow.Ticks,

                    isGroupLeader = false,
                    imports = new HashSet<string> { importId },
                    paths = new HashSet<string> { imagePath }
                };
            }
            else
            {
                hashInfo.paths.Add(imagePath);
                if (hashInfo.imports.Contains(importId))
                {
                    result = ImportCode.IGNORED;
                }
                else
                {
                    hashInfo.imports.Add(importId);
                    result = ImportCode.DUPLICATE;
                }
            }

            db.StoreTempHashInfo(importId, sectionId, hashInfo);
            return result;
        }
    }
}