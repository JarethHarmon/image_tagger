using System.Collections.Generic;
using ImageTagger.Core;
using ImageTagger.Database;

namespace ImageTagger.Metadata
{
    public sealed class ImportInfoAccess
    {
        private static Dictionary<string, ImportInfo> dictImportInfo = new Dictionary<string, ImportInfo>();

        public static void CreateDictionary(IEnumerable<ImportInfo> imports)
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

        public static ImportInfo GetImportInfo(string id)
        {
            if (dictImportInfo?.TryGetValue(id, out var info) ?? false)
                return info;
            // this should not ever be called, and there is an argument to be made that it should just return null instead
            return DatabaseAccess.FindImportInfo(id);
        }
    }
}