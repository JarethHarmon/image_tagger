extends Control

onready var recursively:CheckBox = $margin/vsplit/panel2/margin/vbox/hbox1/recursively
onready var import_name:LineEdit = $margin/vsplit/panel2/margin/vbox/hbox1/import_name
onready var currently_scanning_path:Label = $margin/vsplit/panel2/margin/vbox/hbox1/space/currently_scanning_path

onready var indices:ItemList = $margin/vsplit/path_list/vbox/hsplit2/hsplit21/indices
onready var paths:ItemList = $margin/vsplit/path_list/vbox/hsplit2/hsplit21/paths
onready var types:ItemList = $margin/vsplit/path_list/vbox/hsplit2/hsplit22/types
onready var sizes:ItemList = $margin/vsplit/path_list/vbox/hsplit2/hsplit22/sizes

onready var scan_thread:Thread = Thread.new()
onready var scan_mutex:Mutex = Mutex.new()

var scan_queue:Array = []
var scanner_active:bool = false 
var import_id:String = ""
#var index:int = 0
var image_count:int = 0
var dark_grey:Color = Color(0.14, 0.14, 0.14)

func _ready() -> void:
	#ImageScanner.SetCurrentPathDisplay(currently_scanning_path.get_path())
	get_tree().connect("files_dropped", self, "_files_dropped")
	Signals.connect("start_scan", self, "queue_append")
	Signals.connect("files_selected", self, "_files_selected")
	Signals.connect("folder_selected", self, "_folder_selected")
	reset()

func reset() -> void:
	#index = 0
	image_count = 0
	indices.clear()
	paths.clear()
	types.clear()
	sizes.clear()
	import_name.text = ""

func _files_dropped(file_paths:Array, _screen:int) -> void:
	# instead of how it works now:
	# show the import menu as normal
	# show a popup asking the user if they want to scan the folders recursively
	# then do the same things this function currently does (just with recursion defined by the popup instead of the checkbox)
	var d:Directory = Directory.new()
	var files:Array = []
	var folders:Array = []
	for file in file_paths:
		if d.dir_exists(file): folders.append(file)
		else: files.append(file)
	_files_selected(files)
	for folder in folders: _folder_selected(folder)
	if file_paths.size() > 0: 
		Signals.emit_signal("show_import_menu")

# needs to take into account a blacklist of folders (can be stored on c# side though)
func queue_append(scan_folder:String, recursive:bool=true) -> void:
	if scan_folder == "": return
	if not Directory.new().dir_exists(scan_folder): return
	scan_queue.append([scan_folder, recursive])
	if !scanner_active: start_scanner()
	
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
		ScanManager.SetRecursive(scan[1])
		image_count += ScanManager.StartScan(scan[0])#, scan[1], import_id)
		# there will only every be one scan im progress/waiting to import at a time
		# need to store the count and the array of found paths globally somewhere
		# need to display array of found paths to user (in item list)
		# need to allow user to remove paths/folders, filter by image size/date, etc
		# user should be able to find and scan additional folders while the first set is still scanning
		# once import process starts, user should be able to scan and begin importing new folders while it is running
		#print(image_count)
		OS.delay_msec(50)
	call_deferred("_done")

func _done() -> void:
	if scan_thread.is_active() or scan_thread.is_alive():
		scan_thread.wait_to_finish()
	scanner_active = false
	scan_mutex.unlock()
	print("scan finished")
	
	#var paths:Array = ScanManager.GetPaths()
	#create_item_lists(paths)

func _on_add_folders_button_up() -> void: Signals.emit_signal("add_folders")
func _on_add_files_button_up() -> void: Signals.emit_signal("add_files")

# should populate path list once scanning finishes
func _folder_selected(folder:String) -> void: queue_append(folder, recursively.pressed)
# should populate path list immediately (actually need to obtain size using c# first)
func _files_selected(files:Array) -> void:
	if files.size() == 0: return
	image_count += ScanManager.StartScan(files)#, import_id)
	#var paths_sizes:Array = ImageScanner.GetPathsSizes()
	#create_item_lists(paths_sizes)

#func create_item_lists(paths_sizes:Array) -> void: return
#	for path_size in paths_sizes:
#		var parts:Array = path_size.split("?", false)
#		#print(parts)
#		indices.add_item("  " + String(index))
#		paths.add_item("  " + parts[0])
#		types.add_item("  " + parts[0].get_extension())
#		sizes.add_item("  " + String.humanize_size(parts[1].to_int()))
#		if index % 2 != 0:
##			indices.set_item_custom_bg_color(index, dark_grey)
##			paths.set_item_custom_bg_color(index, dark_grey)
##			types.set_item_custom_bg_color(index, dark_grey)
##			sizes.set_item_custom_bg_color(index, dark_grey)
#
#			indices.set_item_custom_fg_color(index, Color.white)
#			paths.set_item_custom_fg_color(index, Color.white)
#			types.set_item_custom_fg_color(index, Color.white)
#			sizes.set_item_custom_fg_color(index, Color.white)
#		index += 1

func _on_begin_import_button_up() -> void: 
	var _import_name:String = import_name.text if import_name.text != "" else "import"
	Signals.emit_signal("new_import_started", image_count, _import_name)
	reset()

func _on_cancel_import_button_up() -> void:
	reset()
	ScanManager.CancelScan()
	Signals.emit_signal("new_import_canceled")
