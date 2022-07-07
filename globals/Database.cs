using Godot;
using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using Alphaleonis.Win32.Filesystem;
using LiteDB;

using System.Drawing;
using CoenM.ImageHash;
using CoenM.ImageHash.HashAlgorithms;

public class ImageType
{
	// primarily for types with better built-in support, any random types and those the user adds will be assigend 'other'
	public const int jpg = 0;
	public const int png = 1;
	// ...
	public const int other = 7;
}

public class HashInfo
{
	public string komiHash { get; set; }			// the komi64 hash of the image (may use SHA512/256 instead)
	public string gobPath { get; set; }				// the path the file uses if it is copied/moved by the program to a central location
	
	public string diffHash { get; set; }			// the CoenM.ImageHash::DifferenceHash() of the thumbnail
	public int[] colorHash { get; set; }			// the ColorHash() of the thumbnail (manually serialize it to use 8-bit integers if needed)
	
	public int flags { get; set; }					// a FLAG integer used for toggling filter, etc
	public int type { get; set; }					// see ImageType
	public long size { get; set; }					// the length of the file in bytes
	public long creationTime { get; set; }			// the time the file was created in ticks
	public long uploadTime { get; set; }			// the time the file was uploaded to the database in ticks
	
	public HashSet<string> paths { get; set; }
	public HashSet<string> tags { get; set; }
	public Dictionary<string, int> ratings { get; set; }	// rating_name : rating/10
}

public class Hash
{
	public static int[] ColorHash(string path, int accuracy=1)
	{
	// made this up as I went, took a large number of iterations but it works pretty well
	// hash: ~4x faster than DifferenceHash, simi: ~55x slower than DifferenceHash (still ~0.6s/1M comparisons though)
		int[] colors = new int[256/accuracy];
		//int[] colors = new int[766]; // orig
		var bm = new Bitmap(@path, true);
		for (int w = 0; w < bm.Width; w++) {
			for (int h = 0; h < bm.Height; h++) {
				var pixel = bm.GetPixel(w, h);
				//int color = pixel.R + pixel.G + pixel.B; // orig
				int min_color = Math.Min(pixel.B, Math.Min(pixel.R, pixel.G));
				int max_color = Math.Max(pixel.B, Math.Max(pixel.R, pixel.G));
				int color1 = ((min_color/Math.Max(max_color, 1)) * (pixel.R+pixel.G+pixel.B) * pixel.A)/(766*accuracy); 
				int color2 = (w/bm.Width) * (h/bm.Height) * ((min_color/Math.Max(max_color, 1)) * (pixel.R+pixel.G+pixel.B) * pixel.A)/(766*accuracy); 
				int color3 = (pixel.R+pixel.G+pixel.B)/(3*accuracy);
				int color = (color1+color2+color3)/(3*accuracy);
				colors[color]++;
			}
		}
		return colors;
	}
	public static float ColorSimilarity(int[] h1, int[] h2)
	{
		float sum = 0f;
		int count = 0, same = 0, num1 = 0, num2 = 0;
		
		for (int color = 0; color < h1.Length; color++) {
			int sum1 = h1[color], sum2 = h2[color];
			if (sum1 > 0) {
				if (sum2 > 0) {
					same++;
					num2++;
					sum += (sum1 > sum2) ? (float)sum2/sum1 : (float)sum1/sum2;
				}
				num1++;
				count++;
			}
			else if (h2[color] > 0) num2++;
		}
		
		if (num1 == 0 && num2 == 0) return 0f;
		
		float p1 = (num1 > num2) ? (float)num2/num1 : (float)num1/num2;
		float p2 = same/((num1+num2)/2f);
		float p3 = sum/(float)count;
		
		return 100*(p1*p2+p3)/2f;
	}
	public ulong DifferenceHash(string path)
	{
		try {
			var stream = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(path);
			var algo = new DifferenceHash(); // PerceptualHash, DifferenceHash, AverageHash
			return algo.Hash(stream);
		} catch (Exception ex) { GD.Print("Database::GetDifferenceHash() : ", ex); return 0; }
	}
}

public class Database : Node
{
	public Node globals;
	
	public override void _Ready() 
	{
		globals = (Node) GetNode("/root/Globals");
	}
	
	
	
}
