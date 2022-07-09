extends Control


func _input(event:InputEvent) -> void:
	if event is InputEventKey:
		if event.scancode == KEY_F8:
			_notification(MainLoop.NOTIFICATION_WM_QUIT_REQUEST)

func _notification(what) -> void:
	if what == MainLoop.NOTIFICATION_WM_QUIT_REQUEST or what == MainLoop.NOTIFICATION_CRASH or what == MainLoop.NOTIFICATION_WM_GO_BACK_REQUEST:
		# database calls
		# stop threads
		Globals.save_settings()
		print_debug("exiting program")
		get_tree().quit()

func _ready() -> void:
	randomize()
	get_viewport().transparent_bg = true
	call_deferred("_begin")

func _begin() -> void:
 # make and set default metadata folder
	var dir:Directory = Directory.new()
	if Globals.settings.use_default_metadata_path:
		var err:int = dir.make_dir_recursive(Globals.settings.default_metadata_path)
		if err == OK: Database.SetMetadataPath(Globals.settings.default_metadata_path)
	
 # make and set default thumbnail folder
	if Globals.settings.use_default_thumbnail_path:
		var err:int = dir.make_dir_recursive(Globals.settings.default_thumbnail_path)
		#if err == OK: ImageOp.SetThumbnailPath(Globals.settings.default_thumbnail_path)
	
	
