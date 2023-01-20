using System.Collections.Generic;
using System.Linq;
using ImageTagger.Core;

namespace ImageTagger.Metadata
{
    internal sealed class GroupInfoAccess
    {
        private readonly static Dictionary<string, GroupInfo> dictGroupInfo = new Dictionary<string, GroupInfo>();

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
    }
}
