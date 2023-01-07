using Godot;
using ImageTagger.Core;
using ImageTagger.Metadata;
using ImageTagger.Scanner;
using System;

namespace ImageTagger.Managers
{
    // should call ScannerAccess.GetCurrentFolder() every 20ms or so to update label while scanning
    public sealed class ScanManager : Node
    {
        private bool recursive = false;
        public void SetRecursive(bool _recursive) { recursive = _recursive; }

        public int ScanFolder(string folder)
        {
            return ScannerAccess.ScanFolders(folder, recursive);
        }

        public int ScanFiles(string[] files)
        {
            return ScannerAccess.ScanFiles(files);
        }

        public int ScanFoldersAndFiles(string[] folders, string[] files)
        {
            int total = ScannerAccess.ScanFiles(files);
            foreach (string folder in folders)
                total += ScannerAccess.ScanFolders(folder, recursive);
            return total;
        }

        public string CommitScan(string name)
        {
            string[] paths = ScannerAccess.GetScannedPaths();
            var info = new ImportInfo
            {
                Id = Global.CreateImportId(),
                Name = name,

                Total = paths.Length,
                StartTime = DateTime.UtcNow.Ticks,
                Finished = false
            };

            ImportInfoAccess.CreateImport(info, paths);
            // this needs to create a tab button, or be called by something that also handles that
            return info.Id;
        }

        public string[] GetPaths()
        {
            return ScannerAccess.GetScannedPaths();
        }

        public void CancelScan()
        {
            ScannerAccess.Cancel();
        }
    }
}
