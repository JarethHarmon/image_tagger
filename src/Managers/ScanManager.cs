using ImageTagger.Core;
using ImageTagger.Metadata;
using ImageTagger.Scanner;
using System;

namespace ImageTagger.Managers
{
    // should call ScannerAccess.GetCurrentFolder() every 20ms or so to update label while scanning
    public sealed class ScanManager
    {
        private bool recursive = false;
        public void SetRecursive(bool _recursive) { recursive = _recursive; }

        public void StartScan(string folder)
        {
            ScannerAccess.ScanFolders(folder, recursive);
        }

        public void StartScan(string[] files)
        {
            ScannerAccess.ScanFiles(files);
        }

        public void StartScan(string[] folders, string[] files)
        {
            foreach (string folder in folders)
                ScannerAccess.ScanFolders(folder, recursive);
            ScannerAccess.ScanFiles(files);
        }

        public void CommitScan(string name)
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
        }

        public void CancelScan()
        {
            ScannerAccess.Cancel();
        }
    }
}
