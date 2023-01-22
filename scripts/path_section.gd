extends Control

onready var path_list:VBoxContainer = $paths

func _ready() -> void:
	Signals.connect("load_metadata", self, "create_path_buttons")

func create_path_buttons(image_hash:String) -> void:
	if MetadataManager.IncorrectImage(image_hash): return
	var paths:Array = MetadataManager.GetCurrentPaths()
	
	for child in path_list.get_children():
		child.queue_free()
		
	var hsep:HSeparator = HSeparator.new()
	hsep.size_flags_horizontal = SIZE_EXPAND_FILL
	hsep.mouse_filter = Control.MOUSE_FILTER_IGNORE
	path_list.add_child(hsep)
	
	for path in paths:
		var b:Button = Button.new()
		b.text = "  " + path + "  "
		b.connect("pressed", self, "button_pressed", [path])
		b.size_flags_horizontal = SIZE_EXPAND_FILL
		b.align = Button.ALIGN_LEFT
		
		var color:Color = Global.GetRandomGodotPastelColor()
		var sbf:StyleBoxFlat = Globals.make_stylebox(color, 1)
		b.add_color_override("font_color", Color.black) # need to make font thicker
		b.add_stylebox_override("normal", sbf)
		
		path_list.add_child(b)
		hsep = HSeparator.new()
		hsep.size_flags_horizontal = SIZE_EXPAND_FILL
		hsep.mouse_filter = Control.MOUSE_FILTER_IGNORE
		path_list.add_child(hsep)

func button_pressed(path:String) -> void:
	if Input.is_action_pressed("ctrl") and Input.is_action_pressed("shift"): OS.set_clipboard(path.get_file())
	elif Input.is_action_pressed("ctrl"): pass # open in new tab
	elif Input.is_action_pressed("shift"): OS.set_clipboard(path)
	else: OS.set_clipboard(path.get_base_dir())

	# click: copy folder path
	# ctrl-click: open new tab (filtered to the folder)
	# shift-click: copy entire image path
	# ctrl+shift-click: copy file name
	
