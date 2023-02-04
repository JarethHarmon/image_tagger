using LiteDB;
using System;

namespace ImageTagger.ScanImport
{
    internal static class DatabaseAccess
    {
        private static LiteDatabase dbImports;
        private static ILiteCollection<Section> colSections;

        internal static Error Create()
        {
            try
            {
                var mapper = new BsonMapper() { IncludeNonPublic = true };
                dbImports = new LiteDatabase(Global.GetMetadataPath() + "imports.db", mapper);
                colSections = dbImports.GetCollection<Section>("sections");
                return Error.OK;
            }
            catch (LiteException lex)
            {
                Console.WriteLine(lex);
                return Error.Database;
            }
        }

        internal static void Shutdown()
        {
            dbImports?.Dispose();
        }

        internal static void InsertSection(Section section)
        {
            colSections?.Insert(section);
        }
    }
}
