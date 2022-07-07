extends Control


func _input(event:InputEvent) -> void:
	if event is InputEventKey:
		if event.scancode == KEY_F8:
			_notification(MainLoop.NOTIFICATION_WM_QUIT_REQUEST)

func _notification(what) -> void:
	if what == MainLoop.NOTIFICATION_WM_QUIT_REQUEST or what == MainLoop.NOTIFICATION_CRASH or what == MainLoop.NOTIFICATION_WM_GO_BACK_REQUEST:
		# database calls
		# stop threads
		# save settings
		print_debug("exiting program")
		get_tree().quit()

func _ready() -> void:
	randomize()
	get_viewport().transparent_bg = true
	HSplitContainer
