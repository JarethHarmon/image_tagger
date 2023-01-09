using System;
using System.Collections.Generic;
using System.Linq;
using ImageTagger.Core;
using ImageTagger.Database;

namespace ImageTagger.Metadata
{
    internal sealed class ImportInfoAccess
    {
        private readonly static Dictionary<string, ImportInfo> dictImportInfo = new Dictionary<string, ImportInfo>();

        internal static void CreateDictionary(IEnumerable<ImportInfo> imports)
        {
            foreach (var import in imports)
                dictImportInfo[import.Id] = import;

            // create im portInfo for "All" if it does not exist
            if (!dictImportInfo.ContainsKey(Global.ALL))
            {
                var allInfo = new ImportInfo
                {
                    Id = Global.ALL,
                    Name = Global.ALL,
                    Finished = true
                };
                dictImportInfo[Global.ALL] = allInfo;
                DatabaseAccess.InsertImportInfo(allInfo);
            }
        }

        internal static void CreateImport(ImportInfo info, string[] paths)
        {
            if (info.Total == 0) return;

            int numSections = (int)Math.Ceiling((double)info.Total / Global.PROGRESS_SECTION_SIZE);
            int lastSectionSize = info.Total - ((numSections - 1) * Global.PROGRESS_SECTION_SIZE);
            var list = new List<ImportSection>();

            for (int i = 0; i < numSections - 1; i++)
            {
                string[] _paths = new string[Global.PROGRESS_SECTION_SIZE];
                Array.Copy(paths, i * Global.PROGRESS_SECTION_SIZE, _paths, 0, Global.PROGRESS_SECTION_SIZE);

                var id = Global.CreateSectionId();
                var section = new ImportSection
                {
                    Id = id,
                    Paths = _paths
                };

                list.Add(section);
                info.Sections.Add(id);

                // periodically batch insert to prevent storing an entire second copy of the array in memory
                if (i % Global.PROGRESS_SECTION_SIZE == 0)
                {
                    DatabaseAccess.InsertImportSections(list);
                    list.Clear();
                }
            }
            if (lastSectionSize > 0)
            {
                string[] _paths = new string[lastSectionSize]; // << this was causing null paths
                Array.Copy(paths, info.Total - lastSectionSize, _paths, 0, lastSectionSize);

                var id = Global.CreateSectionId();
                var section = new ImportSection
                {
                    Id = id,
                    Paths = _paths
                };
                list.Add(section);
                info.Sections.Add(id);
            }

            if (list.Count > 0)
                DatabaseAccess.InsertImportSections(list);

            dictImportInfo[info.Id] = info;
            DatabaseAccess.InsertImportInfo(info);
        }

        internal static ImportInfo GetImportInfo(string id)
        {
            if (dictImportInfo?.TryGetValue(id, out var info) ?? false)
                return info;
            return null;
        }

        internal static void SetImportInfo(string id, ImportInfo info)
        {
            dictImportInfo[id] = info;
        }

        internal static string[] GetImportIds()
        {
            if (dictImportInfo?.Count == 0) return Array.Empty<string>();
            return dictImportInfo.Keys.ToArray();
        }
    }
}