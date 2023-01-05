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

        public string GetCurrentName()
        {
            return ImageInfoAccess.GetCurrentImageInfo()?.Name ?? string.Empty;
        }

        public int GetCurrentFormat()
        {
            return (int)(ImageInfoAccess.GetCurrentImageInfo()?.ImageType ?? ImageType.ERROR);
        }

        public string[] GetCurrentTags()
        {
            return ImageInfoAccess.GetCurrentImageInfo()?.Tags ?? Array.Empty<string>();
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
        public string CreateTab(string name, TabType type, string importId, string groupId, string tag, string simiHash)
        {
            string id = Global.CreateTabId();
            var info = new TabInfo
            {
                Id = id,
                Name = name,
                TabType = type,
                ImportId = importId,
                GroupId = groupId,
                Tag = tag,
                SimilarityHash = simiHash
            };
            TabInfoAccess.CreateTab(info);
            return id;
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
            return (int)(info?.TabType ?? TabType.DEFAULT);
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
