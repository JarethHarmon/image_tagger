using ImageTagger.Metadata;
using System.Collections.Generic;
using System.Linq;

namespace ImageTagger.Core
{
    public sealed class GroupInfo
    {
        public string Id { get; set; } // need to check if guid or my ids are smaller and change anything with random IDs accordingly (either way guid is much safer collision-wise)
        public string Name { get; set; }
        public string Description { get; set; } // not sure if will keep
        public string CoverImage { get; set; } // the SHA256 hash of the leader/cover image for this group

        public string[] Members { get; set; } //  images in this group (in their user-defined order)
        public string[] Tags { get; set; } // tags assigned to this group as a whole (in practice these will be assigned to the group leader)

        // not 100% sure how this will be used, but most likely it will be cleaner to return Members instead of void
        // would be best to implement this more generically to (hopefully) simplify the code, and allow sorting on other properties
        // note that the sorted order here will be primarily used for the partial group section under the preview window; ie it will 
        //  be separate from the normal thumbnail list; this means that the user could still filter the normal thumbnail list to only include 
        //  members of the current group and then filter/sort those however they wish;; so it is not really necessary to allow sorting on every
        //  possible property for the Members array
        public void Sort(bool natural=true, bool desc=false, bool fullPath=false)
        {
            var members = new HashSet<string>(Members);
            var infos = ImageInfoAccess.GetImageInfo(members);

            if (natural)
            {
                if (desc)
                {
                    if (fullPath) Members = infos.OrderByNatural(x => x.Paths.FirstOrDefault(), desc:true).Select(x => x.Hash).ToArray();
                    else Members = infos.OrderByNatural(x => x.Name, desc:true).Select(x => x.Hash).ToArray();
                }
                else
                {
                    if (fullPath) Members = infos.OrderByNatural(x => x.Paths.FirstOrDefault()).Select(x => x.Hash).ToArray();
                    else Members = infos.OrderByNatural(x => x.Name).Select(x => x.Hash).ToArray();
                }
            }
            else
            {
                if (desc)
                {
                    if (fullPath) Members = infos.OrderByDescending(x => x.Paths.FirstOrDefault()).Select(x => x.Hash).ToArray();
                    else Members = infos.OrderByDescending(x => x.Name).Select(x => x.Hash).ToArray();
                }
                else
                {
                    if (fullPath) Members = infos.OrderBy(x => x.Paths.FirstOrDefault()).Select(x => x.Hash).ToArray();
                    else Members = infos.OrderBy(x => x.Name).Select(x => x.Hash).ToArray();
                }
            }
        }
    }
}
