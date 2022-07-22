using Godot;
using System;

namespace Data
{
	public enum ImportCode { SUCCESS, DUPLICATE, IGNORED, FAILED }
	public enum ImageType { JPG, PNG, APNG, OTHER=7, ERROR }  
	public enum Sort { SHA256, PATH, SIZE, CREATION_TIME, UPLOAD_TIME, DIMENSIONS, TAG_COUNT, RANDOM }
	public enum Order { ASCENDING, DESCENDING }
	public enum Tab { IMPORT_GROUP, IMAGE_GROUP, TAG, SIMILARITY }
	public enum Similarity { COLOR, DIFFERENCE, AVERAGE }
	
	public class Storage : Node
	{
		
	}
}
