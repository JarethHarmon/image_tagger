using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Transactions;

namespace ImageTagger.Scanner
{
    internal sealed class FileRequirements
    {
        public long MinSize { get; set; }
        public long MaxSize { get; set; }
    }

    internal sealed class ScannerAccess
    {
        private static HashSet<string> extensionsToImport = new HashSet<string> { ".png", ".jpeg", ".jpg", ".jfif", ".apng", ".gif", ".webp", ".bmp" };
        private static List<string> blacklistedFolders = new List<string> { "SYSTEM VOLUME INFORMATION", "$RECYCLE.BIN" };
        private static Dictionary<string, HashSet<string>> tempPaths = new Dictionary<string, HashSet<string>>();

        private static bool cancelling = false;
        private static FileRequirements requirements = null;
        private static string currentFolder = string.Empty;
        internal static string GetCurrentFolder() { return currentFolder; }

        internal static void Cancel()
        {
            cancelling = true;
            tempPaths.Clear();
            currentFolder = string.Empty;
        }

        internal static int ScanFiles(string[] paths)
        {
            cancelling = false;
            int imageCount = 0;

            try
            {
                foreach (string path in paths)
                {
                    if (cancelling) return 0;
                    var file = new System.IO.FileInfo(path);
                    if (file.Length < requirements?.MinSize || file.Length > requirements.MaxSize) continue;
                    if (extensionsToImport.Contains(file.Extension.ToLowerInvariant()))
                    {
                        string dir = file.Directory.FullName.Replace("\\", "/");
                        if (tempPaths.ContainsKey(dir)) tempPaths[dir].Add(file.Name);
                        else tempPaths[dir] = new HashSet<string> { file.Name };
                        imageCount++;
                    }
                }
                if (cancelling) return 0;
                return imageCount;
            }
            catch
            {
                return imageCount;
            }
        }

        internal static int ScanFolders(string path, bool recursive)
        {
            cancelling = false;
            var dir = new System.IO.DirectoryInfo(path);
            int imageCount = ScanFolders(dir, recursive);
            if (cancelling) return 0;
            return imageCount;
        }

        private static int ScanFolders(System.IO.DirectoryInfo dir, bool recursive)
        {
            int imageCount = 0;
            cancelling = false;

            try
            {
                currentFolder = dir.FullName;
                var paths = new HashSet<string>();
                foreach (var file in dir.GetFiles())
                {
                    if (cancelling) return 0;
                    if (file.Length < requirements?.MinSize || file.Length > requirements.MaxSize) continue;
                    if (extensionsToImport.Contains(file.Extension.ToLowerInvariant()))
                    {
                        paths.Add(file.Name);
                        imageCount++;
                    }
                }

                tempPaths[dir.FullName.Replace("\\", "/")] = paths;
                if (!recursive) return imageCount;

                var enumeratedFolders = dir.EnumerateDirectories();
                foreach (var folder in enumeratedFolders)
                {
                    if (cancelling) return 0;
                    if (!dir.FullName.Contains(" "))
                    {
                        foreach (string blf in blacklistedFolders)
                            if (dir.FullName.Contains(blf))
                                continue;
                        imageCount += ScanFolders(folder, recursive);
                    }
                }
            }
            catch
            {
                return imageCount;
            }

            if (cancelling) return 0;
            return imageCount;
        }

        internal static string[] GetScannedPaths()
        {
            var list = new List<string>();
            foreach (string folder in tempPaths.Keys.ToArray())
                foreach (string file in tempPaths[folder])
                    list.Add($"{folder}/{file}");
            tempPaths.Clear();
            currentFolder = string.Empty;
            return list.ToArray();
        }
    }
}