using ImageTagger.Importer;
using Godot;

namespace ImageTagger.Managers
{
    public sealed class ImportManager : Node
    {
        public Node signals;
        public DatabaseManager dbm;

        public override void _Ready()
        {
            signals = GetNode<Node>("/root/Signals");
            dbm = GetNode<DatabaseManager>("/root/DatabaseManager");
        }

        public Godot.Image LoadUnsupportedImage(string path)
        {
            (ImageType type, byte[] data) = ImageImporter.LoadUnsupportedImage(path);
            if (type == ImageType.ERROR) return null;
            if (data.Length == 0) return null;

            var image = new Godot.Image();
            if (type == ImageType.PNG) image.LoadPngFromBuffer(data);
            else if (type == ImageType.JPEG) image.LoadJpgFromBuffer(data);
            else return null;

            return image;
        }

        /*=========================================================================================
									      Animations & Large Images
        =========================================================================================*/
        public void LoadGif(string path, string hash)
        {
            Error error = ImageImporter.LoadGif(path, hash);
            signals.Call("emit_signal", "finish_animation", hash);
        }

        public void LoadApng(string path, string hash)
        {
            Error error = ImageImporter.LoadApng(path, hash);
            signals.Call("emit_signal", "finish_animation", hash);
        }

        public void LoadLargeImage(string path, string hash, int columns, int rows)
        {
            Error error = ImageImporter.LoadLargeImage(path, hash, columns, rows);
            signals.Call("emit_signal", "finish_large_image", hash);
        }

        public bool StopLoading(string hash)
        {
            return dbm.IncorrectImage(hash);
        }

        private bool frameOne = true;
        public void SendFrameCount(int count)
        {
            signals.Call("emit_signal", "set_animation_info", count, 24);
            frameOne = true;
        }

        private Godot.ImageTexture GetImageTexture(string data, string hash, string type)
        {
            byte[] bytes = System.Convert.FromBase64String(data);
            var image = new Godot.Image();
            if (type.Equals("jpeg", System.StringComparison.InvariantCultureIgnoreCase))
                image.LoadJpgFromBuffer(bytes);
            else image.LoadPngFromBuffer(bytes);
            if (StopLoading(hash)) return null;

            var texture = new Godot.ImageTexture();
            texture.CreateFromImage(image, 0);
            texture.SetMeta("image_hash", hash);
            return texture;
        }

        public void SendAnimationFrame(string frame)
        {
            string[] sections = frame.Split("?");
            string type = sections[0], hash = sections[1], data = sections[3];
            if (StopLoading(hash)) return;

            float delay;
            if (int.TryParse(sections[2], out int temp)) delay = (float)temp / 1000;
            else delay = (float)double.Parse(sections[2]) / 1000;

            var texture = GetImageTexture(data, hash, type);
            if (StopLoading(hash)) return;
            signals.Call("emit_signal", "add_animation_texture", texture, hash, delay, frameOne);
            frameOne = false;
        }

        private int currentGridIndex = 0;
        public void SendImageTile(string tile)
        {
            string[] sections = tile.Split("?");
            string type = sections[0], hash = sections[1], data = sections[2];
            if (StopLoading(hash)) return;

            var texture = GetImageTexture(data, hash, type);
            if (StopLoading(hash)) return;
            signals.Call("emit_signal", "add_large_image_section", texture, hash, currentGridIndex);
            currentGridIndex++;
        }
    }
}
