using ImageTagger.Core;
using ImageTagger.Database;
using System.Collections.Generic;

namespace ImageTagger.Metadata
{
    public sealed class TabInfoAccess
    {
        private static Dictionary<string, TabInfo> dictTabInfo = new Dictionary<string, TabInfo>();

        public static void CreateDictionary(IEnumerable<TabInfo> tabs)
        {
            foreach (var tab in tabs)
                dictTabInfo[tab.Id] = tab;

            // create importInfo for "All" if it does not exist
            if (!dictTabInfo.ContainsKey(Global.ALL))
            {
                var allInfo = new TabInfo
                {
                    Id = Global.ALL,
                    Name = Global.ALL,
                    TabType = TabType.DEFAULT,
                    ImportId = Global.ALL,
                };
                dictTabInfo[Global.ALL] = allInfo;
                DatabaseAccess.InsertTabInfo(allInfo);
            }
        }

        public static TabInfo GetTabInfo(string id)
        {
            if (dictTabInfo?.TryGetValue(id, out TabInfo info) ?? false)
                return info;
            return null;
        }

        public static TabType GetTabType(string id)
        {
            if (dictTabInfo?.TryGetValue(id, out TabInfo tabInfo) ?? false)
                return tabInfo.TabType;
            return TabType.DEFAULT;
        }

        public static string GetImportId(string id)
        {
            if (dictTabInfo?.TryGetValue(id, out TabInfo tabInfo) ?? false)
                return tabInfo.ImportId;
            return Global.ALL;
        }
    }
}
