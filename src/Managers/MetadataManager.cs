using Godot;
using ImageTagger.Core;
using ImageTagger.Metadata;
using System;
using System.Linq;

namespace ImageTagger.Managers
{
    public sealed class MetadataManager : Node
    {
        /*=========================================================================================
                                                 ImageInfo
        =========================================================================================*/
        public bool IncorrectImage(string hash)
        {
            string _hash = ImageInfoAccess.GetCurrentHash();
            if (_hash.Equals(string.Empty)) return true;
            return !hash.Equals(_hash, StringComparison.InvariantCultureIgnoreCase);
        }

        public void LoadCurrentImageInfo(string hash)
        {
            ImageInfoAccess.SetCurrentImageInfo(hash);
        }

        public string GetCurrentFileSize()
        {
            return ImageInfoAccess.GetCurrentImageInfo()?.Size.ToString() ?? string.Empty;
        }

        public Godot.Vector2 GetCurrentDimensions()
        {
            var info = ImageInfoAccess.GetCurrentImageInfo();
            if (info is null) return new Godot.Vector2(0, 0);
            return new Godot.Vector2(info.Width, info.Height);
        }

        public string[] GetCurrentPaths()
        {
            return ImageInfoAccess.GetCurrentImageInfo()?.Paths.ToArray() ?? Array.Empty<string>();
        }

        public string[] GetCurrentTags()
        {
            return ImageInfoAccess.GetCurrentImageInfo()?.Tags ?? Array.Empty<string>();
        }

        public string[] GetCurrentImports()
        {
            return ImageInfoAccess.GetCurrentImageInfo()?.Imports.ToArray() ?? Array.Empty<string>();
        }

        public string GetCurrentHash()
        {
            return ImageInfoAccess.GetCurrentHash();
        }

        public string GetCurrentName()
        {
            return ImageInfoAccess.GetCurrentImageInfo()?.Name ?? string.Empty;
        }

        public int GetCurrentFormat()
        {
            return (int)(ImageInfoAccess.GetCurrentImageInfo()?.ImageType ?? ImageType.Error);
        }

        public void AddTags(string[] hashes, string[] tags)
        {
            ImageInfoAccess.AddTags(hashes, tags);
        }

        public void RemoveTags(string[] hashes, string[] tags)
        {
            ImageInfoAccess.RemoveTags(hashes, tags);
        }

        public void AddRating(string[] hashes, string rating, int value)
        {
            ImageInfoAccess.AddRating(hashes, rating, value);
        }

        public int GetRating(string hash, string rating)
        {
            return ImageInfoAccess.GetRating(hash, rating);
        }

        /*=========================================================================================
                                                 ImportInfo
        =========================================================================================*/
        public string[] GetSections(string id)
        {
            return ImportInfoAccess.GetImportInfo(id)?.Sections.ToArray() ?? Array.Empty<string>();
        }

        public int GetSuccessCount(string id)
        {
            var info = ImportInfoAccess.GetImportInfo(id);
            if (info is null) return 0;

            if (id.Equals(Global.ALL, StringComparison.InvariantCulture))
                return info.Success;
            return info.Success + info.Duplicate;
        }

        public int GetProcessedCount(string id)
        {
            return ImportInfoAccess.GetImportInfo(id)?.Processed ?? 0;
        }

        public int GetDuplicateCount(string id)
        {
            return ImportInfoAccess.GetImportInfo(id)?.Duplicate ?? 0;
        }

        public int GetTotalCount(string id)
        {
            return ImportInfoAccess.GetImportInfo(id)?.Total ?? 0;
        }

        public bool GetFinished(string id)
        {
            return ImportInfoAccess.GetImportInfo(id)?.Finished ?? true;
        }

        public string[] GetImports()
        {
            return ImportInfoAccess.GetImportIds();
        }

        /*=========================================================================================
                                                  TabInfo
        =========================================================================================*/
        public string CreateTab(string name, int type, string importId, string groupId, string tag, string simiHash)
        {
            string id = Global.CreateTabId();
            var info = new TabInfo
            {
                Id = id,
                Name = name,
                TabType = (TabType)type,
                ImportId = importId,
                GroupId = groupId,
                Tag = tag,
                SimilarityHash = simiHash
            };
            TabInfoAccess.CreateTab(info);
            return id;
        }

        public void DeleteTab(string id)
        {
            TabInfoAccess.DeleteTab(id);
        }

        public string[] GetTabIds()
        {
            return TabInfoAccess.GetTabIds();
        }

        public string GetTabName(string id)
        {
            var info = TabInfoAccess.GetTabInfo(id);
            return info?.Name ?? string.Empty;
        }

        public int GetTabType(string id)
        {
            var info = TabInfoAccess.GetTabInfo(id);
            return (int)(info?.TabType ?? TabType.Default);
        }

        public string GetTabImportId(string id)
        {
            var info = TabInfoAccess.GetTabInfo(id);
            return info?.ImportId ?? Global.ALL;
        }

        public string GetTabSimilarityHash(string id)
        {
            var info = TabInfoAccess.GetTabInfo(id);
            return info?.SimilarityHash ?? string.Empty;
        }
    }
}
