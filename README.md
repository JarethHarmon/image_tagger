# image_tagger

### Information
##### Multithreading
- most tasks are multi-threaded, including: importing images, loading thumbnails, loading full images, querying the database(s).
- I have not encountered any significant threading issues recently, program might freeze when closing though.

##### Tagging
- most of the features related to this are not yet implemented
- basic tagging of images is supported (ie manually typing out tags and pressing enter)
- tags are applied to all selected thumbnails
- tags can be typed/entered in a delimiter-separated format (user can specify delimiter with the small text-entry box)

##### Viewing
- currently supports a few basic shaders and settings (like filter)
- supports rotation with a slider/spinbox (rotates around center of preview section)
- supports click-and-drag and zoom-to-point
- r-click resets preview to default

##### Querying
- user can query by the tags they have added to images 
- user can sort by the number of tags an image has, its sha256 hash, file size, and file creation time
- user can order by ascending/descending
- there is a function to query by image similarity, but the UI for it is not created yet

##### Thumbnails
- there is a slider/spinbox to change the size of the thumbnails
- using mouse + ctrl/shift for multi-selection of thumbnails works
- using arrow keys to scroll and change previewed image works
- using arrow keys + ctrl to select images works, currently shift just does the same thing ctrl does

##### Importing
- images can be imported by clicking the File button, or by drag & dropping images/folders anywhere in the program
- there is a toggle in the import menu to scan folders recursively (currently does not rescan already dropped folders, need to drop them again)
- files are first scanned from the given paths, file information (creationTime, fileSize, etc) is obtained and shown at this time
- importer calculates the sha256 hash of the image and then creates and saves a thumbnail if it is new
- importer then calculates a differenceHash and a colorHash I made using the thumbnail
- importer then stores all of the metadata in the database 
- importer supports multiple simultaneous imports and multiple threads per import
- the thread manager is unreliable at distributing threads and needs to be rewritten

##### Database
- all information related to imports/images is saved to the database
- settings are saved to a separate file, most things that can be toggled currently have a setting that is loaded when the program starts

