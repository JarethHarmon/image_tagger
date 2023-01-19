using System.Collections.Generic;
using System.Linq;
using ImageTagger.Core;

namespace ImageTagger.Metadata
{
    internal sealed class GroupInfoAccess
    {
        private readonly static Dictionary<string, GroupInfo> groups = new Dictionary<string, GroupInfo>();

        internal static GroupInfo GetGroupInfo(string id)
        {
            if (groups.TryGetValue(id, out var group))
                return group;
            return null;
        }

        internal static GroupInfo[] GetGroupInfo(HashSet<string> ids)
        {
            // there is probably a better way to do this
            return groups.Values.Where(x => ids.Contains(x.Id)).ToArray();
        }
    }
}
