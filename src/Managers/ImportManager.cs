using Godot;
using ImageTagger.Importer;
using ImageTagger.Metadata;
using ImageTagger.Database;
using ImageTagger.Core;
using System.Collections.Generic;
using System;
using System.Security.Policy;

namespace ImageTagger.Managers
{
    public sealed class ImportManager : Node
    {
        private const int BULK_IMPORT_SIZE = 100;
        public Node Signals { get; set; }
        public Node Globals { get; set; }
        public MetadataManager MetadataManager { get; set; }

        private static readonly object locker = new object();
        private readonly Dictionary<string, Dictionary<string, ImageInfo>> tempImageInfo = new Dictionary<string, Dictionary<string, ImageInfo>>();
        private static readonly Godot.File file = new Godot.File();
        private static bool stopImporting = false;

        public override void _Ready()
        {
            Globals = GetNode<Node>("/root/Globals");
            Signals = GetNode<Node>("/root/Signals");
            MetadataManager = GetNode<MetadataManager>("/root/MetadataManager");
        }

        public static void Shutdown()
        {
            stopImporting = true;
        }

        /*=========================================================================================
									           Load Images
        =========================================================================================*/

        public Godot.Image LoadUnsupportedImage(string path)
        {
            (ImageType type, byte[] data) = ImageImporter.LoadUnsupportedImage(path);
            if (type == ImageType.Error) return null;
            if (data.Length == 0) return null;

            var image = new Godot.Image();
            if (type == ImageType.Png) image.LoadPngFromBuffer(data);
            else if (type == ImageType.Jpeg) image.LoadJpgFromBuffer(data);
            else return null;

            return image;
        }

        public int LoadGif(string path, string hash)
        {
            var error = ImageImporter.LoadGif(path, hash);
            Signals.Call("emit_signal", "finish_animation", hash);
            return (int)error;
        }

        public int LoadApng(string path, string hash)
        {
            var error = ImageImporter.LoadApng(path, hash);
            Signals.Call("emit_signal", "finish_animation", hash);
            return (int)error;
        }

        public int LoadLargeImage(string path, string hash, int columns, int rows)
        {
            currentGridIndex = 0;
            var error = ImageImporter.LoadLargeImage(path, hash, columns, rows);
            Signals.Call("emit_signal", "finish_large_image", hash);
            return (int)error;
        }

        /*=========================================================================================
									      Animations & Large Images
        =========================================================================================*/
        internal bool StopLoading(string hash)
        {
            return MetadataManager.IncorrectImage(hash);
        }

        private bool frameOne = true;
        internal void SendFrameCount(int count)
        {
            Signals.Call("emit_signal", "set_animation_info", count, 24);
            frameOne = true;
        }

        private Godot.ImageTexture GetImageTexture(string data, string hash, string type)
        {
            byte[] bytes = System.Convert.FromBase64String(data);
            var image = new Godot.Image();
            if (type.Equals("jpeg", System.StringComparison.OrdinalIgnoreCase))
                image.LoadJpgFromBuffer(bytes);
            else image.LoadPngFromBuffer(bytes);
            if (StopLoading(hash)) return null;

            var texture = new Godot.ImageTexture();
            texture.CreateFromImage(image, 0);
            texture.SetMeta("image_hash", hash);
            return texture;
        }

        internal void SendAnimationFrame(string frame)
        {
            string[] sections = frame.Split("?");
            string type = sections[0], hash = sections[1], data = sections[3];
            if (StopLoading(hash)) return;

            float delay;
            if (int.TryParse(sections[2], out int temp)) delay = (float)temp / 1000;
            else delay = (float)double.Parse(sections[2]) / 1000;

            var texture = GetImageTexture(data, hash, type);
            if (StopLoading(hash)) return;
            Signals.Call("emit_signal", "add_animation_texture", texture, hash, delay, frameOne);
            frameOne = false;
        }

        private int currentGridIndex = 0;
        internal void SendImageTile(string tile)
        {
            string[] sections = tile.Split("?");
            string type = sections[0], hash = sections[1], data = sections[2];
            if (StopLoading(hash)) return;

            var texture = GetImageTexture(data, hash, type);
            if (StopLoading(hash)) return;
            Signals.Call("emit_signal", "add_large_image_section", texture, hash, currentGridIndex);
            currentGridIndex++;
        }

        /*=========================================================================================
									             Importing
        =========================================================================================*/
        private void UpdateImportCount(string importId, ImportStatus result)
        {
            lock (locker)
            {
                var info = ImportInfoAccess.GetImportInfo(importId);
                var allInfo = ImportInfoAccess.GetImportInfo(Global.ALL);

                // note: there is a bug in this implementation where it will count the same images a second time if the user deletes only the image_info.db file
                //  this is because importInfo no longer stores the hashes it contains, that information is stored on the images
                if (result == ImportStatus.Success)
                {
                    info.Success++;
                    allInfo.Success++;
                }
                else if (result == ImportStatus.Duplicate)
                {
                    info.Duplicate++;
                }
                else if (result == ImportStatus.Ignored)
                {
                    info.Ignored++;
                }
                else
                {
                    info.Failed++;
                }

                info.Processed++;

                ImportInfoAccess.SetImportInfo(importId, info);
                ImportInfoAccess.SetImportInfo(Global.ALL, allInfo);
            }
        }

        private void StoreTempImageInfo(string importId, ImageInfo info)
        {
            lock (locker)
            {
                if (tempImageInfo.TryGetValue(importId, out var dict))
                {
                    if (dict.TryGetValue(info.Hash, out ImageInfo _info))
                    {
                        info.Merge(_info);
                    }
                }
                else
                {
                    tempImageInfo[importId] = new Dictionary<string, ImageInfo>();
                }

                tempImageInfo[importId][info.Hash] = info;

                if (tempImageInfo[importId].Count >= BULK_IMPORT_SIZE)
                {
                    var infos = new ImportInfo[2]
                    {
                        ImportInfoAccess.GetImportInfo(importId),
                        ImportInfoAccess.GetImportInfo(Global.ALL)
                    };
                    DatabaseAccess.UpsertImageInfo(tempImageInfo[importId].Values);
                    DatabaseAccess.UpdateImportInfo(infos);
                    tempImageInfo[importId].Clear();
                }
            }
        }

        private ImageInfo GetImportingImageInfo(string importId, string hash)
        {
            lock (locker)
            {
                if (tempImageInfo.TryGetValue(importId, out var dict) && dict.TryGetValue(hash, out var info))
                {
                    return info;
                }

                return ImageInfoAccess.GetImageInfo(hash);
            }
        }

        private void CompleteImportSection(string importId, string sectionId)
        {
            lock (locker)
            {
                var info = ImportInfoAccess.GetImportInfo(importId);
                info.Sections.Remove(sectionId);

                if (stopImporting) return;
                // fixes issue with sections not being deleted, should fix desync issue with imports when closing program during import
                // unfortunately, causes import process to stop for a bit due to the lock
                DatabaseAccess.UpdateImportInfo(info);
                DatabaseAccess.DeleteImportSection(sectionId);

                if (info.Sections.Count == 0)
                {
                    if (tempImageInfo.TryGetValue(importId, out var iinfo) && iinfo.Count > 0)
                    {
                        DatabaseAccess.UpsertImageInfo(tempImageInfo[importId].Values);
                    }
                    tempImageInfo.Remove(importId);
                    CompleteImport(importId);
                }
            }
        }

        private void CompleteImport(string importId)
        {
            var infos = new ImportInfo[2]
            {
                ImportInfoAccess.GetImportInfo(importId),
                ImportInfoAccess.GetImportInfo(Global.ALL)
            };

            infos[0].Finished = true;
            infos[0].FinishTime = DateTime.UtcNow.Ticks;

            if (stopImporting) return;
            DatabaseAccess.UpdateImportInfo(infos);
            string[] tabs = TabInfoAccess.GetTabIds(importId);
            Signals.Call("emit_signal", "finish_import_buttons", tabs);
        }

        public void ImportImages(string importId, string sectionId)
        {
            var info = ImportInfoAccess.GetImportInfo(importId);
            if ((info is null || info.Total <= 0 || string.IsNullOrWhiteSpace(importId)) && !string.IsNullOrWhiteSpace(sectionId))
            {
                DatabaseAccess.DeleteImportSection(sectionId);
                return;
            }

            string[] tabs = TabInfoAccess.GetTabIds(importId);
            string[] paths = DatabaseAccess.FindImportSectionPaths(sectionId);
            if (paths.Length == 0) return;

            foreach (string path in paths)
            {
                if (!ImageImporter.FileExists(path))
                {
                    UpdateImportCount(importId, ImportStatus.Failed);
                    Console.WriteLine("file path");
                }
                else
                {
                    ImportStatus result = ImportImage(importId, path);
                    UpdateImportCount(importId, result);
                    Signals.Call("emit_signal", "increment_import_buttons", tabs);
                    if (result == ImportStatus.Success)
                    {
                        Signals.Call("emit_signal", "increment_all_button");
                    }
                }
            }
            if (stopImporting) return;
            CompleteImportSection(importId, sectionId);
        }

        private static ushort[] GetBuckets(ImageImporter.PerceptualHashes phashes)
        {
            ulong hash = phashes.Difference;
            ushort[] result = new ushort[4];
            result[0] = (ushort)(hash & 0xffff);
            hash >>= 16;
            result[1] = (ushort)(hash & 0xffff);
            hash >>= 16;
            result[2] = (ushort)(hash & 0xffff);
            hash >>= 16;
            result[3] = (ushort)(hash & 0xffff);
            return result;
        }

        private static void AddByte(byte color, ref ulong hash)
        {
            if (color > 196) hash |= 255;       // 1111 1111
            else if (color > 160) hash |= 127;  // 0111 1111
            else if (color > 132) hash |= 63;   // 0011 1111
            else if (color > 96) hash |= 31;    // 0001 1111
            else if (color > 64) hash |= 15;    // 0000 1111
            else if (color > 32) hash |= 7;     // 0000 0111
            else if (color > 16) hash |= 3;     // 0000 0011
            else if (color > 0) hash |= 1;      // 0000 0001
            hash <<= 8;
            //WriteUlong(hash);
        }

        private static void AddNibble(byte color, ref ulong hash, bool last=false)
        {
            if (color > 192) hash |= 15;
            else if (color > 128) hash |= 7;
            else if (color > 64) hash |= 3;
            else if (color > 0) hash |= 1;
            if (!last) hash <<= 4;
        }

        private static void WriteUlong(ulong value)
        {
            string tmp = "";
            for (; value > 0; value >>= 1)
                tmp = (value & 0x1).ToString() + tmp;
            Console.WriteLine(tmp);
        }

        private static ulong GetColorHash(int[] colors)
        {
            ulong hash = 0ul;

            // 48 bits
            AddByte((byte)colors[(byte)Colors.Red], ref hash);
            AddByte((byte)colors[(byte)Colors.Green], ref hash);
            AddByte((byte)colors[(byte)Colors.Blue], ref hash);
            AddByte((byte)colors[(byte)Colors.Yellow], ref hash);
            AddByte((byte)colors[(byte)Colors.Cyan], ref hash);
            AddByte((byte)colors[(byte)Colors.Fuchsia], ref hash);

            // 12 bits
            AddNibble((byte)colors[(byte)Colors.Vivid], ref hash);
            AddNibble((byte)colors[(byte)Colors.Neutral], ref hash);
            AddNibble((byte)colors[(byte)Colors.Dull], ref hash, true);

            // 4 bits
            hash <<= 1;
            if (colors[(byte)Colors.Light] > 84) hash |= 1;
            hash <<= 1;
            if (colors[(byte)Colors.Medium] > 84) hash |= 1;
            hash <<= 1;
            if (colors[(byte)Colors.Dark] > 84) hash |= 1;
            hash <<= 1;
            if (colors[(byte)Colors.Alpha] < 255) hash |= 1;

            return hash;
        }

        private ImportStatus ImportImage(string importId, string imagePath)
        {
            var fileInfo = new ImageImporter.FileInfo(imagePath);
            if (fileInfo.Size < 0)
            {
                Console.WriteLine("file size");
                return ImportStatus.Failed;
            }
            string hash = file.GetSha256(imagePath);
            if (hash.Length == 0) return ImportStatus.Failed;

            string thumbPath = $"{Global.GetThumbnailPath()}{hash.Substring(0, 2)}/{hash}.thumb";
            var phashes = new ImageImporter.PerceptualHashes();
            var colors = new ImageImporter.ColorBuckets();
            var result = ImportStatus.Success;
            int numFrames = 0;

            if (stopImporting) return ImportStatus.Failed;
            bool thumbnailExisted = true;
            if (!ImageImporter.FileExists(thumbPath))
            {
                thumbnailExisted = false;
                // will need to check other types that require imageMagick here as well
                if (System.IO.Path.GetExtension(imagePath).Equals(".heic", StringComparison.OrdinalIgnoreCase))
                {
                    (numFrames, phashes, colors) = ImageImporter.SaveThumbnailAndGetPerceptualHashesAndColorsOther(imagePath, thumbPath, Global.THUMBNAIL_SIZE);
                }
                else
                {
                    (numFrames, phashes, colors) = ImageImporter.SaveThumbnailAndGetPerceptualHashesAndColors(imagePath, thumbPath, Global.THUMBNAIL_SIZE);
                }
                if (phashes.Difference == 0 || !ImageImporter.FileExists(thumbPath))
                {
                    return ImportStatus.Failed;
                }
            }

            if (stopImporting) return ImportStatus.Failed;
            var imageInfo = GetImportingImageInfo(importId, hash);
            if (imageInfo is null)
            {
                var imageInfoPart = ImageImporter.GetImageInfoPart(imagePath);
                if (imageInfoPart.ImageType == ImageType.Error)
                {
                    Console.WriteLine("image error");
                    return ImportStatus.Failed;
                }

                if (thumbnailExisted)
                {
                    (numFrames, phashes, colors) = ImageImporter.GetPerceptualHashesAndColors(imagePath, thumbPath);
                    if (phashes.Difference == 0)
                    {
                        Console.WriteLine("diff hash");
                        return ImportStatus.Failed;
                    }
                }

                imageInfo = new ImageInfo
                {
                    Hash = hash,
                    Name = fileInfo.Name,

                    AverageHash = phashes.Average,
                    DifferenceHash = phashes.Difference,
                    WaveletHash = phashes.Wavelet,
                    PerceptualHash = phashes.Perceptual,
                    ColorHash = GetColorHash(colors.Colors),

                    Buckets = GetBuckets(phashes),
                    Colors = colors.Colors,
                    NumFrames = numFrames,

                    Width = imageInfoPart.Width,
                    Height = imageInfoPart.Height,
                    ImageType = imageInfoPart.ImageType,
                    Size = fileInfo.Size,

                    CreationTime = fileInfo.CreationTime,
                    LastWriteTime = fileInfo.LastWriteTime,
                    LastEditTime = DateTime.UtcNow.Ticks,
                    UploadTime = DateTime.UtcNow.Ticks,

                    Imports = new HashSet<string> { importId },
                    Paths = new HashSet<string> { imagePath }
                };
            }
            else
            {
                imageInfo.Paths.Add(imagePath);
                if (imageInfo.Imports.Contains(importId))
                {
                    result = ImportStatus.Ignored;
                }
                else
                {
                    imageInfo.Imports.Add(importId);
                    result = ImportStatus.Duplicate;
                }
            }

            if (stopImporting) return ImportStatus.Failed;
            StoreTempImageInfo(importId, imageInfo);
            return result;
        }
    }
}
