using ImageTagger.Database;

namespace ImageTagger.Metadata
{
    internal sealed class ImageInfoAccess
    {
        internal struct PerceptualHashes
        {
            public ulong average;
            public ulong difference;
            public ulong wavelet;

            public PerceptualHashes(ulong average, ulong difference, ulong wavelet)
            {
                this.average = average;
                this.difference = difference;
                this.wavelet = wavelet;
            }
        }

        internal static PerceptualHashes GetPerceptualHashes(string hash)
        {
            var info = DatabaseAccess.FindImageInfo(hash);
            if (info is null) return new PerceptualHashes();
            return new PerceptualHashes(info.AverageHash, info.DifferenceHash, info.WaveletHash);
        }
    }
}