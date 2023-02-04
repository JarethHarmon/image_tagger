using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;

namespace ImageTagger.ScanImport
{
    internal sealed class FileRequirements
    {
        internal long MinSize { get; set; }
        internal long MaxSize { get; set; }

        internal bool SizeIsBetween(long value)
        {
            if (value > MaxSize) return false;
            if (value < MinSize) return false;
            return true;
        }
    }

    internal static class ScannerAccess
    {
        private static readonly Section section = new Section();
        private static readonly HashSet<string> extensions = new HashSet<string>() { ".png", ".jpeg", ".jpg", ".jfif", ".apng", ".gif", ".webp", ".bmp", ".heic" };
        private static readonly HashSet<string> blacklist = new HashSet<string>(){ "SYSTEM VOLUME INFORMATION", "$RECYCLE.BIN", "TEMP_DELETION" };
        private static readonly Dictionary<string, DirectoryInfo> folderList = new Dictionary<string, DirectoryInfo>();
        private static readonly Dictionary<string, List<string>> fileList = new Dictionary<string, List<string>>();
        private static FileRequirements requirements = null;

        internal static bool Cancel { get; set; }
        internal static string CurrentFolder { get; set; }

        // Setup
        static ScannerAccess()
        {
            Cancel = true;
            CurrentFolder = string.Empty;
        }

        internal static void Clear()
        {
            Cancel = true;
            CurrentFolder = string.Empty;
            folderList.Clear();
            fileList.Clear();
        }

        internal static void SetFileRequirements(FileRequirements _requirements)
        {
            requirements = _requirements;
        }

        // Scanning
        internal static void ScanFolders(string path, bool recursive)
        {
            try
            {
                var dir = new DirectoryInfo(path);
                ScanFolders(dir, recursive);
            }
            catch (DirectoryNotFoundException dnfe)
            {
                Console.WriteLine(dnfe);
            }
            catch (SecurityException se)
            {
                Console.WriteLine(se);
            }
        }

        private static void ScanFolders(DirectoryInfo dir, bool recursive)
        {
            try
            {
                if (Cancel) return;
                string folderName = dir.FullName.Replace('\\', '/');
                folderList[folderName] = dir;
                CurrentFolder = folderName;
                if (!recursive) return;

                foreach (var folder in dir.EnumerateDirectories())
                {
                    if (!dir.FullName.Contains(' '))
                    {
                        if (blacklist.Any(blf => folderName.Contains(blf, StringComparison.OrdinalIgnoreCase))) continue;
                        if (Cancel) return;
                        ScanFolders(folder, recursive); // recursive always true in current implementation
                    }
                }
            }
            catch (DirectoryNotFoundException dnfe)
            {
                Console.WriteLine(dnfe);
            }
            catch (SecurityException se)
            {
                Console.WriteLine(se);
            }
        }

        internal static void ScanFiles(string[] paths)
        {
            try
            {
                foreach (string path in paths)
                {
                    if (Cancel) return;
                    var file = new FileInfo(path);
                    if (requirements?.SizeIsBetween(file.Length) == false) continue;
                    string folderName = file.DirectoryName.Replace('\\', '/');
                    if (blacklist.Any(blf => folderName.Contains(blf, StringComparison.OrdinalIgnoreCase))) continue;
                    if (extensions.ContainsOrdinalIgnore(file.Extension))
                    {
                        if (Cancel) return;
                        if (fileList[folderName] is null) fileList[folderName] = new List<string> { file.Name };
                        else fileList[folderName].Add(file.Name);
                    }
                }
            }
            catch (FileNotFoundException fnfe)
            {
                Console.WriteLine(fnfe);
            }
            catch (SecurityException se)
            {
                Console.WriteLine(se);
            }
        }

        // Importing
        internal static void StartImport()
        {
            if (folderList.Count == 0 && fileList.Count == 0) Clear();
            else
            {
                // this is where I should create an Import object if still want to use that approach
                IterateFolders(Global.GetRandomId());
            }
        }

        private static void IterateFolders(string importId)
        {
            foreach (var fileKV in fileList)
            {
                CreateImportSections(importId, fileKV.Key, fileKV.Value);
            }

            foreach (var folderKV in folderList)
            {
                CurrentFolder = folderKV.Key;
                var files = folderKV.Value.GetFiles();
                if (files is null || files.Length == 0) continue;

                var list = new List<string>(files.Length);
                foreach (var file in files)
                {
                    if (requirements?.SizeIsBetween(file.Length) == false) continue;
                    if (extensions.ContainsOrdinalIgnore(file.Extension))
                    {
                        list.Add(file.Name);
                    }
                }

                if (list.Count == 0) continue;
                CreateImportSections(importId, folderKV.Key, list);
            }
        }

        private static void CreateImportSections(string importId, string folder, List<string> files)
        {
            const int size = Section.SIZE;
            int numSections = (int)Math.Ceiling((decimal)files.Count / size);
            int finalSectionSize = files.Count - ((numSections - 1) * size);
            // .net6/7 ::
            //      ReadOnlySpan<string> filesSpan = CollectionsMarshal.AsSpan(files);

            section.ImportId = importId;
            section.Folder = folder;

            if (numSections > 0)
            {
                section.Files = Section.Base;
                for (int i = 0; i < numSections - 1; i++)
                {
                    int start = i * size;
                    section.Id = Global.GetRandomInt64Id();
                    // .net6/7 :: (leave out for loop below)
                    //      var target = new Span<string>(section.Files, 0, size);
                    //      filesSpan.Slice(start, size).CopyTo(target);

                    for (int o = 0; o < size; o++)
                    {
                        section.Files[o] = files[start + o];
                    }
                    DatabaseAccess.InsertSection(section);
                }
            }

            if (finalSectionSize > 0)
            {
                int start = files.Count - finalSectionSize;
                section.Files = new string[finalSectionSize]; // can leave this out if OK with null inserts into database (have to handle during import)
                section.Id = Global.GetRandomInt64Id();
                for (int i = 0; i < size; i++)
                {
                    section.Files[i] = files[start + i];
                }
                DatabaseAccess.InsertSection(section);
            }
        }
    }
}
