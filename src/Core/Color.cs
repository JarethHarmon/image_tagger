using LiteDB;
using System;

namespace ImageTagger.Core
{
    public sealed class Color
    {
        [BsonIgnore] private static readonly Random random = new Random();
        [BsonIgnore] private static readonly Color[] pastelColors = new Color[]
        {
            PastelRed, PastelGreen, PastelBlue, PastelYellow, PastelFuchsia, PastelCyan,
            PastelOrange, PastelLime, PastelTeal, PastelAquamarine, PastelPurple, PastelPink
        };

        public int R { get; set; }
        public int G { get; set; }
        public int B { get; set; }
        public int A { get; set; }

        public Color(int r, int g, int b)
        {
            R = r;
            G = g;
            B = b;
            A = 255;
        }

        public Color(int r, int g, int b, int a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public Color GetRandomPastelColor()
        {
            return pastelColors[random.Next() % pastelColors.Length];
        }

        public Color MakeRandomColor(bool alpha=false)
        {
            int r = random.Next() % 255;
            int g = random.Next() % 255;
            int b = random.Next() % 255;
            int a = (alpha) ? random.Next() % 255 : 255;
            return new Color(r, g, b, a);
        }

        [BsonIgnore] public static Color Transparent = new Color(0, 0, 0, 0);
        [BsonIgnore] public static Color Black = new Color(0, 0, 0);
        [BsonIgnore] public static Color Grey = new Color(127, 127, 127);
        [BsonIgnore] public static Color White = new Color(255, 255, 255);

        [BsonIgnore] public static Color Red = new Color(255, 0, 0);
        [BsonIgnore] public static Color Green = new Color(0, 255, 0);
        [BsonIgnore] public static Color Blue = new Color(0, 0, 255);
        [BsonIgnore] public static Color Yellow = new Color(255, 255, 0);
        [BsonIgnore] public static Color Fuchsia = new Color(255, 0, 255);
        [BsonIgnore] public static Color Cyan = new Color(0, 255, 255);

        [BsonIgnore] public static Color PastelRed = new Color(255, 150, 150);
        [BsonIgnore] public static Color PastelGreen = new Color(150, 255, 150);
        [BsonIgnore] public static Color PastelBlue = new Color(150, 150, 255);
        [BsonIgnore] public static Color PastelYellow = new Color(255, 255, 200);
        [BsonIgnore] public static Color PastelFuchsia = new Color(255, 200, 255);
        [BsonIgnore] public static Color PastelCyan = new Color(200, 255, 255);
        [BsonIgnore] public static Color PastelOrange = new Color(255, 200, 150);
        [BsonIgnore] public static Color PastelLime = new Color(230, 255, 200);
        [BsonIgnore] public static Color PastelTeal = new Color(200, 255, 230);
        [BsonIgnore] public static Color PastelAquamarine = new Color(200, 230, 255);
        [BsonIgnore] public static Color PastelPurple = new Color(200, 200, 255);
        [BsonIgnore] public static Color PastelPink = new Color(230, 200, 255);
    }
}
