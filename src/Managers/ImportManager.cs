using Godot;
using ImageTagger.Importer;
using ImageTagger.Metadata;
using ImageTagger.Database;
using ImageTagger.Core;
using System.Collections.Generic;
using System;

namespace ImageTagger.Managers
{
    public sealed class ImportManager : Node
    {
        private const int BULK_IMPORT_SIZE = 100;
        public Node signals, globals;
        public MetadataManager mdm;

        private static readonly object locker = new object();
        private Dictionary<string, Dictionary<string, ImageInfo>> tempImageInfo = new Dictionary<string, Dictionary<string, ImageInfo>>();
        private readonly Godot.File file = new Godot.File();

        public override void _Ready()
        {
            globals = GetNode<Node>("/root/Globals");
            signals = GetNode<Node>("/root/Signals");
            mdm = GetNode<MetadataManager>("/root/MetadataManager");
        }

        /*=========================================================================================
									           Load Images
        =========================================================================================*/

        public Godot.Image LoadUnsupportedImage(string path)
        {
            (ImageType type, byte[] data) = ImageImporter.LoadUnsupportedImage(path);
            if (type == ImageType.ERROR) return null;
            if (data.Length == 0) return null;

            var image = new Godot.Image();
            if (type == ImageType.PNG) image.LoadPngFromBuffer(data);
            else if (type == ImageType.JPEG) image.LoadJpgFromBuffer(data);
            else return null;

            return image;
        }

        public void LoadGif(string path, string hash)
        {
            Error error = ImageImporter.LoadGif(path, hash);
            signals.Call("emit_signal", "finish_animation", hash);
        }

        public void LoadApng(string path, string hash)
        {
            Error error = ImageImporter.LoadApng(path, hash);
            signals.Call("emit_signal", "finish_animation", hash);
        }

        public void LoadLargeImage(string path, string hash, int columns, int rows)
        {
            Error error = ImageImporter.LoadLargeImage(path, hash, columns, rows);
            signals.Call("emit_signal", "finish_large_image", hash);
        }

        /*=========================================================================================
									      Animations & Large Images
        =========================================================================================*/
        internal bool StopLoading(string hash)
        {
            return mdm.IncorrectImage(hash);
        }

        private bool frameOne = true;
        internal void SendFrameCount(int count)
        {
            signals.Call("emit_signal", "set_animation_info", count, 24);
            frameOne = true;
        }

        private Godot.ImageTexture GetImageTexture(string data, string hash, string type)
        {
            byte[] bytes = System.Convert.FromBase64String(data);
            var image = new Godot.Image();
            if (type.Equals("jpeg", System.StringComparison.InvariantCultureIgnoreCase))
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
            signals.Call("emit_signal", "add_animation_texture", texture, hash, delay, frameOne);
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
            signals.Call("emit_signal", "add_large_image_section", texture, hash, currentGridIndex);
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
                if (result == ImportStatus.SUCCESS)
                {
                    info.Success++;
                    allInfo.Success++;
                }
                else if (result == ImportStatus.DUPLICATE) info.Duplicate++;
                else if (result == ImportStatus.IGNORED) info.Ignored++;
                else info.Failed++;
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
                else tempImageInfo[importId] = new Dictionary<string, ImageInfo>();
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
                if (tempImageInfo.TryGetValue(importId, out var dict))
                    if (dict.TryGetValue(hash, out var info))
                        return info;
                return ImageInfoAccess.GetImageInfo(hash);
            }
        }

        private void CompleteImportSection(string importId, string sectionId)
        {
            lock (locker)
            {
                var info = ImportInfoAccess.GetImportInfo(importId);
                info.Sections.Remove(sectionId);

                if (info.Sections.Count == 0)
                {
                    lock (locker)
                    {
                        if (tempImageInfo.TryGetValue(importId, out var iinfo))
                        {
                            if (iinfo.Count > 0)
                            {
                                DatabaseAccess.UpsertImageInfo(tempImageInfo[importId].Values);
                            }
                        }
                        tempImageInfo.Remove(importId);
                    }
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

            DatabaseAccess.UpdateImportInfo(infos);
            string[] tabs = TabInfoAccess.GetTabIds(importId);
            signals.Call("emit_signal", "finish_import_buttons", tabs);
        }

        public void ImportImages(string importId, string sectionId)
        {
            var info = ImportInfoAccess.GetImportInfo(importId);
            if ((info is null || info.Total <= 0 || importId.Equals(string.Empty)) && !sectionId.Equals(string.Empty))
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
                    UpdateImportCount(importId, ImportStatus.FAILED);
                    Console.WriteLine("file path");
                }
                else
                {
                    ImportStatus result = ImportImage(importId, path);
                    UpdateImportCount(importId, result);
                    signals.Call("emit_signal", "increment_import_buttons", tabs);
                    if (result == ImportStatus.SUCCESS)
                    {
                        signals.Call("emit_signal", "increment_all_button");
                    }
                }
            }
            CompleteImportSection(importId, sectionId);
        }

        private ImportStatus ImportImage(string importId, string imagePath)
        {
            var fileInfo = new ImageImporter.FileInfo(imagePath);
            if (fileInfo.Size < 0)
            {
                Console.WriteLine("file size");
                return ImportStatus.FAILED;
            }
            string hash = file.GetSha256(imagePath);
            if (hash.Length == 0) return ImportStatus.FAILED;

            string thumbPath = $"{Global.GetThumbnailPath()}{hash.Substring(0, 2)}/{hash}.thumb";
            var phashes = new ImageImporter.PerceptualHashes();
            var colors = new ImageImporter.ColorBuckets();
            var result = ImportStatus.SUCCESS;

            bool thumbnailExisted = true;
            if (!ImageImporter.FileExists(thumbPath))
            {
                thumbnailExisted = false;
                (phashes, colors) = ImageImporter.SaveThumbnailAndGetPerceptualHashesAndColors(imagePath, thumbPath, Global.THUMBNAIL_SIZE);
                if (phashes.Difference == 0 || !ImageImporter.FileExists(thumbPath))
                {
                    Console.WriteLine("file path or diff hash");
                    return ImportStatus.FAILED;
                }
            }

            var imageInfo = GetImportingImageInfo(importId, hash);
            if (imageInfo is null)
            {
                var imageInfoPart = ImageImporter.GetImageInfoPart(imagePath);
                if (imageInfoPart.ImageType == ImageType.ERROR)
                {
                    Console.WriteLine("image error");
                    return ImportStatus.FAILED;
                }

                if (thumbnailExisted)
                {
                    (phashes, colors) = ImageImporter.GetPerceptualHashesAndColors(thumbPath);
                    if (phashes.Difference == 0)
                    {
                        Console.WriteLine("diff hash");
                        return ImportStatus.FAILED;
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

                    Red = colors.Red,
                    Green = colors.Green,
                    Blue = colors.Blue,
                    Alpha = colors.Alpha,
                    Light = colors.Light,
                    Dark = colors.Dark,

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
                    result = ImportStatus.IGNORED;
                }
                else
                {
                    imageInfo.Imports.Add(importId);
                    result = ImportStatus.DUPLICATE;
                }
            }

            //StoreTempImageInfo(importId, sectionId, imageInfo);
            StoreTempImageInfo(importId, imageInfo);
            return result;
        }
    }
}
