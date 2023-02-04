using Godot;
using System.Threading.Tasks;

namespace ImageTagger.ScanImport
{
    public sealed class ScannerManager : Node
    {
        public static bool Recursive { get; set; }

        public static void ScanFolder(string folder)
        {
            _ = ScanFolderAsync(folder);
        }

        private static async Task ScanFolderAsync(string folder)
        {
            await Task.Run(() => ScannerAccess.ScanFolders(folder, Recursive));
        }

        public static void ScanFiles(string[] files)
        {
            _ = ScanFilesAsync(files);
        }

        private static async Task ScanFilesAsync(string[] files)
        {
            await Task.Run(() => ScannerAccess.ScanFiles(files));
        }

        public static void ScanFoldersAndFiles(string[] folders, string[] files)
        {
            _ = ScanFilesAsync(files);
            foreach (string folder in folders)
                _ = ScanFolderAsync(folder);
        }

        public static void StartImport()
        {
            ScannerAccess.StartImport();
        }

        public static void CancelImport()
        {
            ScannerAccess.Clear();
        }
    }
}
