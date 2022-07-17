# image_tagger

### Information
##### Multithreading
- Most tasks are multi-threaded, including: importing images, loading thumbnails, loading full images, querying the database(s).
- I have not encountered any significant threading issues recently, program might freeze when closing though.

##### Tagging
- Most of the features related to this are not yet implemented.
- Basic tagging of images is supported (ie manually typing out tags and pressing enter).
- Tags are applied to all selected thumbnails.
- Tags can be typed/entered in a delimiter-separated format (user can specify delimiter with the small text-entry box).

##### Viewing
- Currently supports a few basic shaders and settings (like filter).
- Supports rotation with a slider/spinbox (rotates around center of preview section).
- Supports click-and-drag and zoom-to-point.
- R-click resets preview to default.

##### Querying
- User can query by the tags they have added to images.
- User can sort by the number of tags an image has, its sha256 hash, file size, and file creation time.
- User can order by ascending/descending.
- Uhere is a function to query by image similarity, but the UI for it is not created yet.

##### Thumbnails
- There is a slider/spinbox to change the size of the thumbnails.
- Using mouse + ctrl/shift for multi-selection of thumbnails works.
- Using arrow keys to scroll and change previewed image works.
- Using arrow keys + ctrl to select images works, currently shift just does the same thing ctrl does.

##### Importing
- Images can be imported by clicking the File button, or by drag & dropping images/folders anywhere in the program.
- There is a toggle in the import menu to scan folders recursively (currently does not rescan already dropped folders, need to drop them again).
- Files are first scanned from the given paths, file information (creationTime, fileSize, etc) is obtained and shown at this time.
- Importer calculates the sha256 hash of the image and then creates and saves a thumbnail if it is new.
- Importer then calculates a differenceHash and a colorHash I made using the thumbnail.
- Importer then stores all of the metadata in the database.
- Importer supports multiple simultaneous imports and multiple threads per import.
- The thread manager is unreliable at distributing threads and needs to be rewritten.
- In-progress imports are saved and written to the database when the user closes the program.
- In-progress imports are automatically resumed when the user starts the program.

##### Database
- All information related to imports/images is saved to the database.
- Settings are saved to a separate file, most things that can be toggled currently have a setting that is loaded when the program starts.

