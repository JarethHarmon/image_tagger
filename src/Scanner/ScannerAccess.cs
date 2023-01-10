using System;
using System.Collections.Generic;
using System.Linq;

namespace ImageTagger.Scanner
{
    internal sealed class FileRequirements
    {
        public long MinSize { get; set; }
        public long MaxSize { get; set; }
    }

    internal sealed class ScannerAccess
    {
        private readonly static HashSet<string> extensionsToImport = new HashSet<string> { ".png", ".jpeg", ".jpg", ".jfif", ".apng", ".gif", ".webp", ".bmp", ".heic" };
        private readonly static List<string> blacklistedFolders = new List<string> { "SYSTEM VOLUME INFORMATION", "$RECYCLE.BIN", "TEMP_DELETION" };
        private readonly static Dictionary<string, HashSet<string>> tempPaths = new Dictionary<string, HashSet<string>>();

        private static bool cancelling = false;
        private readonly static FileRequirements requirements = null; // probably should not be readonly, unless I change check to use default values instead
        private static string currentFolder = string.Empty;
        internal static string GetCurrentFolder() { return currentFolder; }

        internal static void Cancel()
        {
            cancelling = true;
            tempPaths.Clear();
            currentFolder = string.Empty;
        }

        internal static int GetImageCount()
        {
            int count = 0;
            foreach (var section in tempPaths.Values)
                count += section.Count;
            return count;
        }

        internal static int ScanFiles(string[] paths)
        {
            cancelling = false;
            try
            {
                foreach (string path in paths)
                {
                    if (cancelling) return 0;
                    var file = new System.IO.FileInfo(path);
                    if (file.Length < requirements?.MinSize || file.Length > requirements?.MaxSize) continue;
                    if (extensionsToImport.Contains(file.Extension.ToLowerInvariant()))
                    {
                        string dir = file.Directory.FullName.Replace("\\", "/");
                        currentFolder = dir;
                        if (tempPaths.ContainsKey(dir)) tempPaths[dir].Add(file.Name);
                        else tempPaths[dir] = new HashSet<string> { file.Name };
                    }
                }
                if (cancelling) return 0;
                return GetImageCount();
            }
            catch
            {
                return GetImageCount();
            }
        }

        internal static int ScanFolders(string path, bool recursive)
        {
            cancelling = false;
            var dir = new System.IO.DirectoryInfo(path);
            ScanFolders(dir, recursive);
            if (cancelling) return 0;
            return GetImageCount();
        }

        private static void ScanFolders(System.IO.DirectoryInfo dir, bool recursive)
        {
            cancelling = false;
            try
            {
                if (cancelling) return;
                currentFolder = dir.FullName;

                var paths = new HashSet<string>();
                foreach (var file in dir.GetFiles())
                {
                    if (cancelling) return;
                    if (file.Length < requirements?.MinSize || file.Length > requirements?.MaxSize) continue;
                    if (extensionsToImport.Contains(file.Extension.ToLowerInvariant()))
                    {
                        paths.Add(file.Name);
                    }
                }

                tempPaths[dir.FullName.Replace("\\", "/")] = paths;
                if (!recursive) return;

                var enumeratedFolders = dir.EnumerateDirectories();
                bool continuing = false;
                foreach (var folder in enumeratedFolders)
                {
                    if (cancelling) return;
                    continuing = false;
                    if (!dir.FullName.Contains(" "))
                    {
                        foreach (string blf in blacklistedFolders)
                        {
                            if (dir.FullName.Contains(blf))
                            {
                                continuing = true;
                            }
                        }

                        if (continuing) continue;
                        ScanFolders(folder, recursive);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        internal static string[] GetScannedPaths()
        {
            var list = new List<string>();
            foreach (string folder in tempPaths.Keys.ToArray())
            {
                foreach (string file in tempPaths[folder])
                {
                    list.Add($"{folder}/{file}");
                }
            }

            tempPaths.Clear();
            currentFolder = string.Empty;
            return list.ToArray();
        }
    }
}