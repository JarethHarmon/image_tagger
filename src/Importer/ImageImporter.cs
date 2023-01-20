using System;
using ImageMagick;
using ImageTagger.Database;
using Python.Runtime;

namespace ImageTagger.Importer
{
    internal sealed class ImageImporter
    {
        private const int MAX_BYTES_FOR_APNG_CHECK = 256;
        private const string acTL = "6163544C";

        /*=========================================================================================
									         Initialization
        =========================================================================================*/
        private static IntPtr state;
        internal static Error StartPython()
        {
            try
            {
                // will need to handle this better if I intend to release on other OS
                const string pyPathWin10 = "./lib/python-3.10.7-embed-amd64/python310.dll";
                Environment.SetEnvironmentVariable("PYTHONNET_PYDLL", pyPathWin10);
                PythonEngine.Initialize();
                state = PythonEngine.BeginAllowThreads();
                return Error.OK;
            }
            catch
            {
                return Error.Python;
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
            public ulong Perceptual;

            public PerceptualHashes(string hashes)
            {
                string[] _hashes = hashes.Split('?');
                if (_hashes.Length != 4)
                {
                    Average = 0;
                    Difference = 0;
                    Wavelet = 0;
                    Perceptual = 0;
                }
                else
                {
                    ulong.TryParse(_hashes[0], out Average);
                    ulong.TryParse(_hashes[1], out Wavelet);
                    ulong.TryParse(_hashes[2], out Difference);
                    ulong.TryParse(_hashes[3], out Perceptual);
                }
            }

            public PerceptualHashes(ulong average, ulong difference, ulong wavelet, ulong perceptual)
            {
                Average = average;
                Difference = difference;
                Wavelet = wavelet;
                Perceptual = perceptual;
            }
        }

        internal static PerceptualHashes GetPerceptualHashes(string hash)
        {
            if (hash?.Equals(string.Empty) ?? true) return new PerceptualHashes();
            var info = DatabaseAccess.FindImageInfo(hash);
            if (info is null) return new PerceptualHashes();
            return new PerceptualHashes(info.AverageHash, info.DifferenceHash, info.WaveletHash, info.PerceptualHash);
        }

        internal struct ColorBuckets
        {
            public int[] Colors;

            public ColorBuckets(string _colors)
            {
                string[] temp = _colors.Split('?');
                if (temp?.Length == 13)
                {
                    Colors = new int[13];
                    for (int i = 0; i < temp.Length; i++)
                    {
                        if (!int.TryParse(temp[i], out Colors[i])) Colors[i] = 0;
                    }
                }
                else
                {
                    Colors = new int[13];
                }
            }
        }

        private static (int, PerceptualHashes, ColorBuckets) ProcessImportResult(string result)
        {
            string[] sections = result.Split('!');
            if (sections.Length != 3) return (0, new PerceptualHashes(), new ColorBuckets());
            int.TryParse(sections[0], out int numFrames);
            return (numFrames, new PerceptualHashes(sections[1]), new ColorBuckets(sections[2]));
        }

        internal static (int, PerceptualHashes, ColorBuckets) SaveThumbnailAndGetPerceptualHashesAndColors(string imagePath, string thumbPath, int thumbSize)
        {
            const string pyScript = "pil_import";
            string result = string.Empty;
            using (Py.GIL())
            {
                try
                {
                    dynamic script = Py.Import(pyScript);
                    dynamic _result = script.save_webp_thumbnail(imagePath, thumbPath, thumbSize);
                    result = (string)_result;
                }
                catch (PythonException pex)
                {
                    Console.WriteLine(pex);
                    return (0, new PerceptualHashes(), new ColorBuckets());
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    return (0, new PerceptualHashes(), new ColorBuckets());
                }
            }
            return ProcessImportResult(result);
        }

        internal static (int, PerceptualHashes, ColorBuckets) GetPerceptualHashesAndColors(string imagePath, string thumbPath)
        {
            const string pyScript = "pil_import";
            string result = string.Empty;
            using (Py.GIL())
            {
                try
                {
                    dynamic script = Py.Import(pyScript);
                    dynamic _result = script.initialize(imagePath, thumbPath);
                    result = (string)_result;
                }
                catch (PythonException pex)
                {
                    Console.WriteLine(pex);
                    return (0, new PerceptualHashes(), new ColorBuckets());
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    return (0, new PerceptualHashes(), new ColorBuckets());
                }
            }
            return ProcessImportResult(result);
        }

        internal static (int, PerceptualHashes, ColorBuckets) SaveThumbnailAndGetPerceptualHashesAndColorsOther(string imagePath, string thumbPath, int thumbSize)
        {
            try
            {
                var image = new MagickImage(imagePath);

                // to avoid wasting more time on broken images, this should only check relevant images
                // another option is to use the file extension, but that is obviously less reliable
                if (image.Format != MagickFormat.Heic) return (0, new PerceptualHashes(), new ColorBuckets());

                image.Strip();
                image.Thumbnail(thumbSize, thumbSize);
                //image.Resize(thumbSize, thumbSize);
                image.Format = MagickFormat.WebP;
                image.Write(thumbPath);

                return GetPerceptualHashesAndColors(imagePath, thumbPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return (0, new PerceptualHashes(), new ColorBuckets());
            }
        }

        /*=========================================================================================
									                IO
        =========================================================================================*/
        internal static bool FileExists(string path)
        {
            return (path.Length < Global.MAX_PATH_LENGTH)
                ? System.IO.File.Exists(path)
                : Alphaleonis.Win32.Filesystem.File.Exists(path);
        }

        private static bool TryLoadFile(string path, out byte[] data)
        {
            if (!FileExists(path))
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

                    ImageType = ImageType.Other;
                    Width = info.Width;
                    Height = info.Height;

                    string format = info.Format.ToString().ToUpperInvariant().Replace("JPG", "JPEG").Replace("JFIF", "JPEG");
                    if (format.Equals("jpeg", StringComparison.InvariantCultureIgnoreCase)) ImageType = ImageType.Jpeg;
                    else if (format.Equals("png", StringComparison.InvariantCultureIgnoreCase)) ImageType = ImageType.Png;
                    else if (format.Equals("gif", StringComparison.InvariantCultureIgnoreCase)) ImageType = ImageType.Gif;
                    else if (format.Equals("webp", StringComparison.InvariantCultureIgnoreCase)) ImageType = ImageType.Webp;
                }
                catch
                {
                    ImageType = ImageType.Error;
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
                    ImageType = ImageType.Error;
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
                ? new ImageInfoPart(path, ImageType.Apng)
                : new ImageInfoPart(path);
        }

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
                        return (ImageType.Png, image.ToByteArray());
                    }
                    else
                    {
                        image.Format = MagickFormat.Jpeg;
                        image.Quality = 95;
                        return (ImageType.Jpeg, image.ToByteArray());
                    }
                }
                catch
                {
                    return (ImageType.Error, Array.Empty<byte>());
                }
            }
            return (ImageType.Error, Array.Empty<byte>());
        }

        /*=========================================================================================
									      Animations & Large Images
        =========================================================================================*/
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
                catch (PythonException pex)
                {
                    Console.WriteLine(pex);
                    return Error.Python;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    return Error.Generic;
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
                catch (PythonException pex)
                {
                    Console.WriteLine(pex);
                    return Error.Python;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    return Error.Generic;
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
                catch (PythonException pex)
                {
                    Console.WriteLine(pex);
                    return Error.Python;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    return Error.Generic;
                }
            }
        }
    }
}