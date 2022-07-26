extends Control

export (NodePath) onready var thumbnails = get_node(thumbnails)
export (NodePath) onready var importer = get_node(importer)
export (NodePath) onready var previewer = get_node(previewer)

func _input(event:InputEvent) -> void:
	if event is InputEventKey:
		if event.scancode == KEY_F8:
			_notification(MainLoop.NOTIFICATION_WM_QUIT_REQUEST)

var quitting:bool = false # prevents trying to quit multiple times (was causing errors)
func _notification(what) -> void:
	if quitting: return
	if what == MainLoop.NOTIFICATION_WM_QUIT_REQUEST or what == MainLoop.NOTIFICATION_CRASH or what == MainLoop.NOTIFICATION_WM_GO_BACK_REQUEST:
		quitting = true
		OS.set_window_minimized(true)
		thumbnails.stop_thumbnail_threads()
		importer.cancel_all()
		previewer.stop_threads()
		Database.SaveInProgressPaths()
		Database.CheckpointGroupDB()
		Database.CheckpointHashDB()
		Database.CheckpointImportDB()
		Database.CheckpointTagDB()
		Database.Destroy()
		Globals.save_settings()
		print_debug("exiting program")
		get_tree().quit()

func _ready() -> void:	
	randomize()
	get_viewport().transparent_bg = true
	Signals.connect("settings_loaded", self, "_begin")

func _begin() -> void:
 # make and set default metadata folder
	var dir:Directory = Directory.new()
	var err:int = dir.make_dir_recursive(Globals.settings.metadata_path)
	if err == OK: Database.SetMetadataPath(Globals.settings.metadata_path)
	
 # make and set default thumbnail folder
	err = dir.make_dir_recursive(Globals.settings.thumbnail_path)
	if err == OK: ImageImporter.SetThumbnailPath(Globals.settings.thumbnail_path)
	create_thumbnail_folders()
	
  # create database
	if (Database.Create() != OK): _notification(MainLoop.NOTIFICATION_WM_QUIT_REQUEST)
	# load import groups from database (and other list metadata)
	Database.CreateAllInfo()
	Database.LoadImportInfo()
	Database.LoadTabInfo()
	Database.LoadInProgressPaths()
	
	Signals.emit_signal("import_info_load_finished")	

func create_thumbnail_folders() -> void:
	var arr:Array = ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "a", "b", "c", "d", "e", "f"]
	var dir:Directory = Directory.new()
	var thumb_path = Globals.settings.thumbnail_path
	for hex1 in arr:
		for hex2 in arr:
			dir.make_dir_recursive(thumb_path + hex1 + hex2 + "/")
	
