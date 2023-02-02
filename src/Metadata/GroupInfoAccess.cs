using System.Collections.Generic;
using System.Linq;
using ImageTagger.Core;

namespace ImageTagger.Metadata
{
    internal static class GroupInfoAccess
    {
        private readonly static Dictionary<string, GroupInfo> dictGroupInfo = new Dictionary<string, GroupInfo>();

        internal static void CreateDictionary(IEnumerable<GroupInfo> groups)
        {
            foreach (var group in groups)
                dictGroupInfo.Add(group.Id, group);
        }

        internal static GroupInfo GetGroupInfo(string id)
        {
            if (dictGroupInfo.TryGetValue(id, out var info))
                return info;
            return null;
        }

        internal static GroupInfo[] GetGroupInfo(HashSet<string> ids)
        {
            // there is probably a better way to do this
            return dictGroupInfo.Values.Where(x => ids.Contains(x.Id)).ToArray();
        }

        internal static void SetGroupInfo(string id, GroupInfo info)
        {
            dictGroupInfo[id] = info;
        }
    }
}
