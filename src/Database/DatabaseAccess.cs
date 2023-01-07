using System;
using System.Collections.Generic;
using LiteDB;
using ImageTagger.Core;
using ImageTagger.Metadata;

namespace ImageTagger.Database
{
    internal sealed class DatabaseAccess
    {
        private static LiteDatabase dbImageInfo, dbImportInfo;
        private static ILiteCollection<ImageInfo> colImageInfo;
        private static ILiteCollection<ImportInfo> colImportInfo;
        private static ILiteCollection<ImportSection> colImportSection;
        private static ILiteCollection<TabInfo> colTabInfo;

        internal static Error Create()
        {
            try
            {
                string metadataPath = Global.GetMetadataPath();
                BsonMapper.Global.Entity<ImageInfo>().Id(x => x.Hash); // still need to test if int id is better; last test was inconclusive
                dbImageInfo = new LiteDatabase(metadataPath + "image_info.db");
                dbImportInfo = new LiteDatabase(metadataPath + "import_info.db");

                colImageInfo = dbImageInfo.GetCollection<ImageInfo>("images");
                colImportInfo = dbImportInfo.GetCollection<ImportInfo>("imports");
                colImportSection = dbImportInfo.GetCollection<ImportSection>("sections");
                colTabInfo = dbImportInfo.GetCollection<TabInfo>("tabs");

                return Error.OK;
            }
            catch
            {
                // log error
                return Error.DATABASE; // maybe should switch to an int flag and return database and IO as the flags
            }
        }

        internal static Error Setup()
        {
            if (colImageInfo is null) return Error.DATABASE;
            if (colImportInfo is null) return Error.DATABASE;
            if (colTabInfo is null) return Error.DATABASE;

            var imports = colImportInfo.FindAll();
            ImportInfoAccess.CreateDictionary(imports);

            var tabs = colTabInfo.FindAll();
            TabInfoAccess.CreateDictionary(tabs);

            return Error.OK;
        }

        internal static void Shutdown()
        {
            dbImageInfo?.Dispose();
            dbImportInfo?.Dispose();
        }

        /* ===================================================================================
                                            ImageInfo 
        =================================================================================== */
        internal static ILiteQueryable<ImageInfo> GetImageInfoQuery()
        {
            return colImageInfo?.Query();
        }

        internal static ImageInfo FindImageInfo(string hash)
        {
            return colImageInfo?.FindById(hash);
        }

        internal static void UpdateImageInfo(ImageInfo info)
        {
            colImageInfo?.Update(info);
        }

        internal static void UpdateImageInfo(IEnumerable<ImageInfo> infos)
        {
            colImageInfo?.Update(infos);
        }

        internal static void UpsertImageInfo(ImageInfo info)
        {
            colImageInfo?.Upsert(info);
        }

        internal static void UpsertImageInfo(IEnumerable<ImageInfo> infos)
        {
            colImageInfo?.Upsert(infos);
        }

        /* ===================================================================================
                                            ImportInfo 
        =================================================================================== */
        internal static ImportInfo FindImportInfo(string id)
        {
            return colImportInfo?.FindById(id);
        }

        internal static void InsertImportInfo(ImportInfo info)
        {
            colImportInfo?.Insert(info);
        }

        internal static void InsertImportInfo(IEnumerable<ImportInfo> infos)
        {
            colImportInfo?.Insert(infos);
        }

        internal static void UpdateImportInfo(ImportInfo info)
        {
            colImportInfo?.Update(info);
        }

        internal static void UpdateImportInfo(IEnumerable<ImportInfo> infos)
        {
            colImportInfo?.Update(infos);
        }

        internal static void DeleteImportInfo(string id)
        {
            colImportInfo?.Delete(id);
        }

        /* ===================================================================================
                                           ImportSection 
        =================================================================================== */
        internal static string[] FindImportSectionPaths(string id)
        {
            return colImportSection?.FindById(id)?.Paths ?? Array.Empty<string>();
        }

        internal static void InsertImportSections(IEnumerable<ImportSection> sections)
        {
            colImportSection?.Insert(sections);
        }

        internal static void DeleteImportSection(string id)
        {
            colImportSection?.Delete(id);
        }

        /* ===================================================================================
                                              TabInfo 
        =================================================================================== */
        internal static ILiteQueryable<TabInfo> GetTabInfoQuery()
        {
            return colTabInfo?.Query();
        }

        internal static void InsertTabInfo(TabInfo tabInfo)
        {
            colTabInfo?.Insert(tabInfo);
        }

        internal static void DeleteTabInfo(string id)
        {
            colTabInfo?.Delete(id);
        }
    }
}