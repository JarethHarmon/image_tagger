using Godot;

namespace ImageTagger.Managers
{
    // need to test if I can just use static functions instead
    // also this does get added to the scene tree, but should do so itself instead of always being in the scene tree
    public sealed class PythonManager : Node
    {
        private readonly ImportManager importer;
        public PythonManager()
        {
            var mainloop = Engine.GetMainLoop();
            var scenetree = mainloop as SceneTree;
            scenetree.Root.AddChild(this);
            importer = GetNode<ImportManager>("/root/ImportManager");
        }

        public void SendFrameCount(dynamic d_framecount)
        {
            int framecount = (int)d_framecount;
            importer.SendFrameCount(framecount);
        }

        public void SendAnimationFrame(dynamic d_frame)
        {
            string frame = (string)d_frame;
            importer.SendAnimationFrame(frame);
        }

        public void SendImageTile(dynamic d_tile)
        {
            string tile = (string)d_tile;
            importer.SendImageTile(tile);
        }

        public bool StopLoading(dynamic d_hash)
        {
            string hash = (string)d_hash;
            return importer.StopLoading(hash);
        }
    }
}
