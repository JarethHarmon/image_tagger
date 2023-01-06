using System;
using ImageMagick;
using ImageTagger.Database;
using Python.Runtime;

namespace ImageTagger.Importer
{
    internal sealed class ImageImporter
    {
        private const int MAX_BYTES_FOR_APNG_CHECK = 256;
        private const string acTL = "acTL";

        /*=========================================================================================
									         Initialization
        =========================================================================================*/
        private static IntPtr state;
        internal static Error StartPython(string executableDirectory)
        {
            try
            {
                // will need to handle this better if I intend to release on other OS
                string pyPath = executableDirectory + @"lib\python-3.10.7-embed-amd64\python310.dll";
                System.Environment.SetEnvironmentVariable("PYTHONNET_PYDLL", pyPath);
                PythonEngine.Initialize();
                state = PythonEngine.BeginAllowThreads();
                return Error.OK;
            }
            catch
            {
                return Error.PYTHON;
            }
        }

        internal static void Shutdown()
        {
            PythonEngine.EndAllowThreads(state);
            PythonEngine.Shutdown();
        }

        /*=========================================================================================
									         Hashing & Thumbnails
        =========================================================================================*/
        internal struct PerceptualHashes
        {
            public ulong Average;
            public ulong Difference;
            public ulong Wavelet;

            public PerceptualHashes(ulong average, ulong difference, ulong wavelet)
            {
                Average = average;
                Difference = difference;
                Wavelet = wavelet;
            }
        }

        internal static PerceptualHashes GetPerceptualHashes(string hash)
        {
            if (hash?.Equals(string.Empty) ?? true) return new PerceptualHashes();
            var info = DatabaseAccess.FindImageInfo(hash);
            if (info is null) return new PerceptualHashes();
            return new PerceptualHashes(info.AverageHash, info.DifferenceHash, info.WaveletHash);
        }

        internal struct ColorBuckets
        {
            public int Red;
            public int Green;
            public int Blue;
            public int Alpha;
            public int Light;
            public int Dark;

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

                    Red = colors[0];
                    Green = colors[1];
                    Blue = colors[2];
                    Alpha = colors[3];
                    Light = colors[4];
                    Dark = colors[5];
                }
                else
                {
                    Red = 0;
                    Green = 0;
                    Blue = 0;
                    Alpha = 0;
                    Light = 0;
                    Dark = 0;
                }
            }
        }

        internal static (PerceptualHashes, ColorBuckets) SaveThumbnailAndGetPerceptualHashesAndColors(string imagePath, string thumbPath, int thumbSize)
        {
            const string pyScript = "pil_thumbnail_perceptual_hashes";
            using (Py.GIL())
            {
                try
                {
                    dynamic script = Py.Import(pyScript);
                    script.save_webp_thumbnail(imagePath, thumbPath, thumbSize);

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
                catch
                {
                    return (new PerceptualHashes(), new ColorBuckets());
                }
            }
        }

        internal static (PerceptualHashes, ColorBuckets) GetPerceptualHashesAndColors(string thumbPath)
        {
            const string pyScript = "pil_thumbnail_perceptual_hashes";
            using (Py.GIL())
            {
                try
                {
                    dynamic script = Py.Import(pyScript);
                    script.initialize(thumbPath);

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
                catch
                {
                    return (new PerceptualHashes(), new ColorBuckets());
                }
            }
        }

        /*=========================================================================================
									                IO
        =========================================================================================*/
        internal static bool FileDoesNotExist(string path)
        {
            return (path.Length < Global.MAX_PATH_LENGTH)
                ? !System.IO.File.Exists(path)
                : !Alphaleonis.Win32.Filesystem.File.Exists(path);
        }

        private static bool TryLoadFile(string path, out byte[] data)
        {
            if (FileDoesNotExist(path))
            {
                data = Array.Empty<byte>();
                return false;
            }

            try
            {
                if (path.Length < Global.MAX_PATH_LENGTH) data = System.IO.File.ReadAllBytes(path);
                else data = Alphaleonis.Win32.Filesystem.File.ReadAllBytes(path);
                return true;
            }
            catch
            {
                data = Array.Empty<byte>();
                return false;
            }
        }

        internal static bool IsImageCorrupt(string path)
        {
            try
            {
                var image = (path.Length < Global.MAX_PATH_LENGTH)
                    ? new MagickImage(path)
                    : new MagickImage(TryLoadFile(path, out byte[] data) ? data : Array.Empty<byte>());
                return false;
            }
            catch
            {
                return true;
            }
        }

        internal sealed class FileInfo
        {
            public long Size;
            public long CreationTime;
            public long LastWriteTime;
            public string Name;

            public FileInfo(string path)
            {
                try
                {
                    if (path.Length < Global.MAX_PATH_LENGTH)
                    {
                        var info = new System.IO.FileInfo(path);
                        Size = info.Length;
                        CreationTime = info.CreationTimeUtc.Ticks;
                        LastWriteTime = info.LastWriteTimeUtc.Ticks;
                        Name = info.Name.Substring(0, info.Name.Length - info.Extension.Length);
                    }
                    else
                    {
                        var info = new Alphaleonis.Win32.Filesystem.FileInfo(path);
                        Size = info.Length;
                        CreationTime = info.CreationTimeUtc.Ticks;
                        LastWriteTime = info.LastWriteTimeUtc.Ticks;
                        Name = info.Name.Substring(0, info.Name.Length - info.Extension.Length);
                    }
                }
                catch
                {
                    Size = -1;
                    CreationTime = -1;
                    LastWriteTime = -1;
                    Name = string.Empty;
                }
            }
        }

        internal struct ImageInfoPart
        {
            public ImageType ImageType;
            public int Width;
            public int Height;

            public ImageInfoPart(string path)
            {
                try
                {
                    var info = (path.Length < Global.MAX_PATH_LENGTH)
                        ? new MagickImageInfo(path)
                        : new MagickImageInfo(TryLoadFile(path, out byte[] data) ? data : Array.Empty<byte>());

                    ImageType = ImageType.OTHER;
                    Width = info.Width;
                    Height = info.Height;

                    string format = info.Format.ToString().ToUpperInvariant().Replace("JPG", "JPEG").Replace("JFIF", "JPEG");
                    if (format.Equals("jpeg", StringComparison.InvariantCultureIgnoreCase)) ImageType = ImageType.JPEG;
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

            public ImageInfoPart(string path, ImageType type)
            {
                try
                {
                    var info = (path.Length < Global.MAX_PATH_LENGTH)
                        ? new MagickImageInfo(path)
                        : new MagickImageInfo(TryLoadFile(path, out byte[] data) ? data : Array.Empty<byte>());

                    ImageType = type;
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

        private static bool IsApng(string path)
        {
            try
            {
                var fs = new System.IO.FileStream(path, System.IO.FileMode.Open);
                byte[] data = new byte[MAX_BYTES_FOR_APNG_CHECK];
                int bytesRead = fs.Read(data, 0, MAX_BYTES_FOR_APNG_CHECK);

                fs?.Dispose();
                if (data.Length == 0) return false;
                if (bytesRead < MAX_BYTES_FOR_APNG_CHECK) return false;

                string hex = BitConverter.ToString(data).Replace("-", string.Empty);
                if (hex?.Equals(string.Empty) ?? true) return false;
                return hex.Contains(acTL);
            }
            catch
            {
                return false;
            }
        }

        internal static ImageInfoPart GetImageInfoPart(string path)
        {
            return (IsApng(path))
                ? new ImageInfoPart(path, ImageType.APNG)
                : new ImageInfoPart(path);
        }

        // have 3rd script create the image instead to avoid using Godot here
        internal static (ImageType, byte[]) LoadUnsupportedImage(string path)
        {
            if (TryLoadFile(path, out byte[] data))
            {
                try
                {
                    var image = new MagickImage(data);

                    // the idea was to load images saved with the wrong format, but whether this makes sense at all depends on how 
                    // imageMagick assigns the Format property
                    if (image.Format == MagickFormat.Jpeg || image.Format == MagickFormat.Jpg)
                    {
                        image.Format = MagickFormat.Png;
                        return (ImageType.PNG, image.ToByteArray());
                    }
                    else
                    {
                        image.Format = MagickFormat.Jpeg;
                        image.Quality = 95;
                        return (ImageType.JPEG, image.ToByteArray());
                    }
                }
                catch
                {
                    return (ImageType.ERROR, Array.Empty<byte>());
                }
            }
            return (ImageType.ERROR, Array.Empty<byte>());
        }

        /*=========================================================================================
									      Animations & Large Images
        =========================================================================================*/
        // have whatever calls this emit the signal instead
        internal static Error LoadGif(string path, string hash)
        {
            const string pyScript = "pil_load_animation";
            using (Py.GIL())
            {
                try
                {
                    dynamic script = Py.Import(pyScript);
                    script.get_gif_frames(path, hash);
                    return Error.OK;
                }
                catch (PythonException)
                {
                    return Error.PYTHON;
                }
                catch
                {
                    return Error.GENERIC;
                }
            }
        }

        internal static Error LoadApng(string path, string hash)
        {
            const string pyScript = "pil_load_animation";
            using (Py.GIL())
            {
                try
                {
                    dynamic script = Py.Import(pyScript);
                    script.get_apng_frames(path, hash);
                    return Error.OK;
                }
                catch (PythonException)
                {
                    return Error.PYTHON;
                }
                catch
                {
                    return Error.GENERIC;
                }
            }
        }

        internal static Error LoadLargeImage(string path, string hash, int columns, int rows)
        {
            const string pyScript = "pil_load_large_image";
            using (Py.GIL())
            {
                try
                {
                    dynamic script = Py.Import(pyScript);
                    script.load_large_image(path, hash, columns, rows);
                    return Error.OK;
                }
                catch (PythonException)
                {
                    return Error.PYTHON;
                }
                catch
                {
                    return Error.GENERIC;
                }
            }
        }
    }
}