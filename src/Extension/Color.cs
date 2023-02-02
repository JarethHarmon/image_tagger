using System;

namespace ImageTagger.Extension
{
    public sealed class Color
    {
        public static readonly Color Transparent = new Color(0, 0, 0, 0);
        public static readonly Color Black = new Color(0, 0, 0);
        public static readonly Color Grey = new Color(127, 127, 127);
        public static readonly Color White = new Color(255, 255, 255);

        public static readonly Color Red = new Color(255, 0, 0);
        public static readonly Color Green = new Color(0, 255, 0);
        public static readonly Color Blue = new Color(0, 0, 255);
        public static readonly Color Yellow = new Color(255, 255, 0);
        public static readonly Color Fuchsia = new Color(255, 0, 255);
        public static readonly Color Cyan = new Color(0, 255, 255);

        public static readonly Color PastelRed = new Color(255, 150, 150);
        public static readonly Color PastelGreen = new Color(150, 255, 150);
        public static readonly Color PastelBlue = new Color(150, 150, 255);
        public static readonly Color PastelYellow = new Color(255, 255, 200);
        public static readonly Color PastelFuchsia = new Color(255, 200, 255);
        public static readonly Color PastelCyan = new Color(200, 255, 255);
        public static readonly Color PastelOrange = new Color(255, 200, 150);
        public static readonly Color PastelLime = new Color(230, 255, 200);
        public static readonly Color PastelTeal = new Color(200, 255, 230);
        public static readonly Color PastelAquamarine = new Color(200, 230, 255);
        public static readonly Color PastelPurple = new Color(200, 200, 255);
        public static readonly Color PastelPink = new Color(230, 200, 255);

        private static readonly Random random = new Random();
        private static readonly Color[] pastelColors = new Color[]
        {
            PastelRed, PastelGreen, PastelBlue, PastelYellow, PastelFuchsia, PastelCyan,
            PastelOrange, PastelLime, PastelTeal, PastelAquamarine, PastelPurple, PastelPink
        };

        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
        public byte A { get; set; }

        public static void Test()
        {
            var color = PastelGreen;
            Console.WriteLine(color.ToString());
            int _color = color.GetIntColor();
            Console.WriteLine(_color);
            var __color = new Color(_color);
            Console.WriteLine(__color.ToString());
        }

        public Color(int color)
        {
            uint _color = (uint)color;
            A = (byte)(_color & 0xff);
            _color >>= 8;
            B = (byte)(_color & 0xff);
            _color >>= 8;
            G = (byte)(_color & 0xff);
            _color >>= 8;
            R = (byte)_color;
        }

        public Color(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
            A = 255;
        }

        public Color(byte r, byte g, byte b, byte a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public int GetIntColor()
        {
            uint tmp = R;
            tmp <<= 8;
            tmp |= G;
            tmp <<= 8;
            tmp |= B;
            tmp <<= 8;
            tmp |= A;
            return (int)tmp;
        }

        public override string ToString()
        {
            return $"{R}, {G}, {B}, {A}";
        }

        public static Color GetRandomPastelColor()
        {
            return pastelColors[random.Next() % pastelColors.Length];
        }

        public static Color MakeRandomColor(bool alpha=false)
        {
            byte r = (byte)random.Next(0, 256);
            byte g = (byte)random.Next(0, 256);
            byte b = (byte)random.Next(0, 256);
            byte a = (alpha) ? (byte)random.Next(0, 256) : (byte)255;
            return new Color(r, g, b, a);
        }
    }
}
