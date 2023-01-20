using Godot;
using ImageTagger.Core;
using ImageTagger.Metadata;
using ImageTagger.Scanner;
using System;
using System.Threading.Tasks;

namespace ImageTagger.Managers
{
    public sealed class ScanManager : Node
    {
        private bool recursive = false;
        public void SetRecursive(bool _recursive) { recursive = _recursive; }

        public int ScanFolder(string folder)
        {
            return ScanFolderAsync(folder).Result;
        }

        private async Task<int> ScanFolderAsync(string folder)
        {
            return await Task.Run(() => ScannerAccess.ScanFolders(folder, recursive));
        }

        public int ScanFiles(string[] files)
        {
            return ScanFilesAsync(files).Result;
        }

        private async Task<int> ScanFilesAsync(string[] files)
        {
            return await Task.Run(() => ScannerAccess.ScanFiles(files));
        }

        public int ScanFoldersAndFiles(string[] folders, string[] files)
        {
            return ScanFoldersAndFilesAsync(folders, files).Result;
        }

        private async Task<int> ScanFoldersAndFilesAsync(string[] folders, string[] files)
        {
            int total = await Task.Run(() => ScannerAccess.ScanFiles(files));
            foreach (string folder in folders)
                total += await Task.Run(() => ScannerAccess.ScanFolders(folder, recursive));
            return total;
        }

        public string CommitScan(string name)
        {
            string[] paths = ScannerAccess.GetScannedPaths();
            var info = new ImportInfo
            {
                Name = name,
                Total = paths.Length,
                StartTime = DateTime.UtcNow.Ticks,
                Finished = false
            };
            ImportInfoAccess.CreateImport(info, paths);
            // using async here, the import process never actually started, though things were inserted into the database correctly
            //CreateImport(info, paths);
            return info.Id;
        }

        private async void CreateImport(ImportInfo info, string[] paths)
        {
            await Task.Run(() => ImportInfoAccess.CreateImport(info, paths));
        }

        public string[] GetPaths()
        {
            return ScannerAccess.GetScannedPaths();
        }

        public string GetCurrentFolder()
        {
            return ScannerAccess.GetCurrentFolder();
        }

        public void CancelScan()
        {
            ScannerAccess.Cancel();
        }
    }
}
