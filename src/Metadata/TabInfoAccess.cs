using System;
using System.Collections.Generic;
using System.Linq;
using ImageTagger.Core;
using ImageTagger.Database;

namespace ImageTagger.Metadata
{
    internal sealed class TabInfoAccess
    {
        private static Dictionary<string, TabInfo> dictTabInfo = new Dictionary<string, TabInfo>();

        internal static void CreateDictionary(IEnumerable<TabInfo> tabs)
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

        internal static TabInfo GetTabInfo(string id)
        {
            if (dictTabInfo.TryGetValue(id, out TabInfo info))
                return info;
            return null;
        }

        internal static TabType GetTabType(string id)
        {
            if (dictTabInfo.TryGetValue(id, out TabInfo tabInfo))
                return tabInfo.TabType;
            return TabType.DEFAULT;
        }

        internal static string GetImportId(string id)
        {
            if (dictTabInfo.TryGetValue(id, out TabInfo tabInfo))
                return tabInfo.ImportId;
            return Global.ALL;
        }

        internal static string[] GetTabIds()
        {
            return dictTabInfo.Keys.ToArray();
        }

        internal static string[] GetTabIds(string importId)
        {
            if (importId.Equals(string.Empty)) return Array.Empty<string>();
            var query = DatabaseAccess.GetTabInfoQuery();
            query = query.Where(x => x.ImportId.Equals(importId));
            return query.Select(x => x.Id).ToArray();
        }

    }
}
