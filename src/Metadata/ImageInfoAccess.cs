using ImageTagger.Core;
using ImageTagger.Database;

namespace ImageTagger.Metadata
{
    internal sealed class ImageInfoAccess
    {
        private static ImageInfo currentImageInfo;

        internal static string GetCurrentHash()
        {
            return currentImageInfo?.Hash ?? string.Empty;
        }

        internal static ImageInfo GetImageInfo(string hash)
        {
            if (currentImageInfo?.Hash.Equals(hash, System.StringComparison.InvariantCultureIgnoreCase) ?? false)
                return currentImageInfo;
            return DatabaseAccess.FindImageInfo(hash);
        }
    }
}