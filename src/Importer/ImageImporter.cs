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
            public int Red;
            public int Green;
            public int Blue;
            public int Yellow;
            public int Cyan;
            public int Fuchsia;
            public int Light;
            public int Dark;
            public int Alpha;

            public ColorBuckets(string _colors)
            {
                string[] temp = _colors.Split('?');
                if (temp?.Length == 9)
                {
                    int[] colors = new int[9];
                    for (int i = 0; i < temp.Length; i++)
                    {
                        if (!int.TryParse(temp[i], out colors[i])) colors[i] = 0;
                    }

                    Red = colors[0];
                    Green = colors[1];
                    Blue = colors[2];
                    Yellow = colors[3];
                    Cyan = colors[4];
                    Fuchsia = colors[5];
                    Light = colors[6];
                    Dark = colors[7];
                    Alpha = colors[8];
                }
                else
                {
                    Red = 0;
                    Green = 0;
                    Blue = 0;
                    Yellow = 0;
                    Cyan = 0;
                    Fuchsia = 0;
                    Light = 0;
                    Dark = 0;
                    Alpha = 0;
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
                    dynamic _result = script.save_webp_thumbnail(imagePath, thumbPath, thumbSize);
                    string result = (string) _result;

                    string[] sections = result.Split('!');
                    if (sections.Length != 2) return (new PerceptualHashes(), new ColorBuckets());

                    string[] hashes = sections[0].Split('?');
                    if (hashes.Length != 4) return (new PerceptualHashes(), new ColorBuckets());
                    ulong.TryParse(hashes[0], out ulong average);
                    ulong.TryParse(hashes[1], out ulong wavelet);
                    ulong.TryParse(hashes[2], out ulong difference);
                    ulong.TryParse(hashes[3], out ulong perceptual);

                    return (new PerceptualHashes(average, difference, wavelet, perceptual), new ColorBuckets(sections[1]));
                }
                catch (PythonException pex)
                {
                    Console.WriteLine(pex);
                    return (new PerceptualHashes(), new ColorBuckets());
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
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
                    dynamic _result = script.initialize(thumbPath);
                    string result = (string)_result;

                    string[] sections = result.Split('!');
                    if (sections.Length != 2) return (new PerceptualHashes(), new ColorBuckets());

                    string[] hashes = sections[0].Split('?');
                    if (hashes.Length != 4) return (new PerceptualHashes(), new ColorBuckets());
                    ulong.TryParse(hashes[0], out ulong average);
                    ulong.TryParse(hashes[1], out ulong wavelet);
                    ulong.TryParse(hashes[2], out ulong difference);
                    ulong.TryParse(hashes[3], out ulong perceptual);

                    return (new PerceptualHashes(average, difference, wavelet, perceptual), new ColorBuckets(sections[1]));
                }
                catch (PythonException pex)
                {
                    Console.WriteLine(pex);
                    return (new PerceptualHashes(), new ColorBuckets());
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    return (new PerceptualHashes(), new ColorBuckets());
                }
            }
        }

        internal static (PerceptualHashes, ColorBuckets) SaveThumbnailAndGetPerceptualHashesAndColorsOther(string imagePath, string thumbPath, int thumbSize)
        {
            try
            {
                var image = new MagickImage(imagePath);

                // to avoid wasting more time on broken images, this should only check relevant images
                // another option is to use the file extension, but that is obviously less reliable
                if (image.Format != MagickFormat.Heic) return (new PerceptualHashes(), new ColorBuckets());

                image.Strip();
                image.Thumbnail(thumbSize, thumbSize);
                //image.Resize(thumbSize, thumbSize);
                image.Format = MagickFormat.WebP;
                image.Write(thumbPath);

                return GetPerceptualHashesAndColors(thumbPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return (new PerceptualHashes(), new ColorBuckets());
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