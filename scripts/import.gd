extends Control

# this script only handles scanning the directories
# it will call the relevant c# functions to do so
# then it will present a list of scanned directories to the user and allow them
# to filter by image size, remove folders/paths, scan additional folders/paths, filter
# by file extension, etc (which will all be done on another script)

onready var recursively:CheckBox = $margin/vsplit/panel2/margin/vbox/hbox1/recursively

onready var indices:ItemList = $margin/vsplit/path_list/margin/vbox/hsplit2/hsplit21/indices
onready var paths:ItemList = $margin/vsplit/path_list/margin/vbox/hsplit2/hsplit21/paths
onready var types:ItemList = $margin/vsplit/path_list/margin/vbox/hsplit2/hsplit22/types
onready var sizes:ItemList = $margin/vsplit/path_list/margin/vbox/hsplit2/hsplit22/sizes

onready var scan_thread:Thread = Thread.new()
onready var scan_mutex:Mutex = Mutex.new()

var scan_queue:Array = []
var scanner_active:bool = false 

var index:int = 0

func _ready() -> void:
	get_tree().connect("files_dropped", self, "_files_dropped")
	Signals.connect("start_scan", self, "queue_append")
	Signals.connect("files_selected", self, "_files_selected") # need to append them directly to the path_list
	Signals.connect("folder_selected", self, "_folder_selected")

func reset() -> void:
	index = 0
	indices.clear()
	paths.clear()
	types.clear()
	sizes.clear()

# once the user starts importing, the settings are finalized and the importer closes
# they can open the importer again, but it will start a new separate import
# the previous import will continue in the background and the user can manually pause/stop it
func _files_dropped(file_paths:Array, _screen:int) -> void:
	var d:Directory = Directory.new()
	var files:Array = []
	for file in file_paths:
		if d.dir_exists(file): _folder_selected(file)
		else: files.append(file)
	_files_selected(files)
	if file_paths.size() > 0: 
		Signals.emit_signal("show_import_menu")
		#print(file)
		# need to check whether the importer is visible
		# if it is, add these paths to the path list
		# otherwise open the importer for a new import to begin
		# Option 1:
			# just populate the list with any paths that are folders or images
			# allow user to select folders and press a button to scan them recursively
		# Option 2:
			# automatically scan any dropped folders for the presence of images
			# maybe do both, with a toggle setting for automatic scanning 

# needs to take into accounr a blacklist of folders (can be stored on c# side though)
func queue_append(scan_folder:String, recursive:bool=true) -> void:
	if scan_folder == "": return
	if not Directory.new().dir_exists(scan_folder): return
	scan_queue.append([scan_folder, recursive])
	if !scanner_active: start_scanner()

# protecting scan_queue with a mutex is not necessary since the scanner will only be single threaded
# (it finishes even large folders in a few milliseconds)
func start_scanner() -> void:
	if scanner_active: return
	if scan_mutex.try_lock() != OK: return
	if scan_thread.is_alive(): return
	scan_mutex.lock()
	scan_thread.start(self, "_thread")
	scanner_active = true

func _thread() -> void:
	while not scan_queue.empty():
		var scan:Array = scan_queue.pop_front()
		var image_count:int = ImageScanner.ScanDirectories(scan[0], scan[1])
		# there will only every be one scan im progress/waiting to import at a time
		# need to store the count and the array of found paths globally somewhere
		# need to display array of found paths to user (in item list)
		# need to allow user to remove paths/folders, filter by image size/date, etc
		# user should be able to find and scan additional folders while the first set is still scanning
		# once import process starts, user should be able to scan and begin importing new folders while it is running
		print(image_count)
		OS.delay_msec(50)
	call_deferred("_done")

func _done() -> void:
	if scan_thread.is_active() or scan_thread.is_alive():
		scan_thread.wait_to_finish()
	scanner_active = false
	scan_mutex.unlock()
	print("scan finished")
	
	var paths_sizes:Array = ImageScanner.GetPathsSizes()
	for path_size in paths_sizes:
		var parts:Array = path_size.split("?", false)
		indices.add_item("  " + String(index))
		paths.add_item("  " + parts[0])
		types.add_item("  " + parts[0].get_extension())
		sizes.add_item("  " + String.humanize_size(parts[1].to_int()))
		index += 1

func _on_add_folders_button_up() -> void: Signals.emit_signal("add_folders")
func _on_add_files_button_up() -> void: Signals.emit_signal("add_files")

# should populate path list once scanning finishes
func _folder_selected(folder:String) -> void: queue_append(folder, recursively.pressed)
# should populate path list immediately (actually need to obtain size using c# first)
func _files_selected(files:Array) -> void:
	if files.size() == 0: return
	var count:int = ImageScanner.ScanFiles(files)
	var paths_sizes:Array = ImageScanner.GetPathsSizes()
	for path_size in paths_sizes:
		var parts:Array = path_size.split("?", false)
		indices.add_item("  " + String(index))
		paths.add_item("  " + parts[0])
		types.add_item("  " + parts[0].get_extension())
		sizes.add_item("  " + String.humanize_size(parts[1].to_int()))
		index += 1

func _on_begin_import_button_up() -> void: 
	reset()
	Signals.emit_signal("new_import_started")

