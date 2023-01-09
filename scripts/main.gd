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
		importer.cancel_manager()
		importer.cancel_IS_thread()
		previewer.stop_threads()
		Global.Shutdown()		
		print_debug("exiting program")
		get_tree().quit()

func _ready() -> void:	
 # initial setup
	randomize()
	get_viewport().transparent_bg = true
	var dir:Directory = Directory.new()

	if Global.Setup(Globals._get_program_directory()) != Globals.Error.OK: _notification(NOTIFICATION_WM_QUIT_REQUEST)
	Signals.emit_signal("settings_loaded") 
	Signals.emit_signal("import_info_load_finished") # consider changing to "setup_finished"
	
 # make and set default metadata folder
	var metadata_path:String = Global.GetMetadataPath()
	if (dir.make_dir_recursive(metadata_path) != OK): return
	
 # make and set default thumbnail folder
	var thumbnail_path:String = Global.GetThumbnailPath()
	if (dir.make_dir_recursive(thumbnail_path) != OK): return

 # create thumbnail folders
	var arr:Array = ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "a", "b", "c", "d", "e", "f"]
	for hex1 in arr:
		for hex2 in arr:
			dir.make_dir_recursive(thumbnail_path.plus_file(hex1 + hex2 + "/"))
