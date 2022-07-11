using Godot;
using System;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

public class ImageImporter : Node
{
	public string GetRandomID(int num_bytes)
	{
		try{
			byte[] bytes = new byte[num_bytes];
			var rng = new RNGCryptoServiceProvider();
			rng.GetBytes(bytes);
			rng.Dispose();
			return BitConverter.ToString(bytes).Replace("-", "");
		}
		catch (Exception ex) { GD.Print("Database::GetRandomID() : ", ex); return ""; } 
	}
	
	public string CreateImportID()
	{
		return "I" + GetRandomID(8); // get 64bit ID
	}
}
