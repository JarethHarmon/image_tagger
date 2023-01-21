extends Control

onready var path_list:VBoxContainer = $paths

func _ready() -> void:
	Signals.connect("create_path_buttons", self, "create_path_buttons")

func create_path_buttons(sha256:String, paths:Array) -> void:
	for child in path_list.get_children():
		child.queue_free()
		
	var hsep:HSeparator = HSeparator.new()
	hsep.size_flags_horizontal = SIZE_EXPAND_FILL
	hsep.mouse_filter = Control.MOUSE_FILTER_IGNORE
	path_list.add_child(hsep)
	
	for path in paths:
		var b:Button = Button.new()
		b.text = path
		b.connect("pressed", self, "button_pressed", [path])
		b.size_flags_horizontal = SIZE_EXPAND_FILL
		
		var color:Color = Globals.make_color()
		var sbf:StyleBoxFlat = Globals.make_stylebox(color, 1.5)
		b.add_color_override("font_color", Color.black) # need to make font thicker
		b.add_stylebox_override("normal", sbf)
		
		path_list.add_child(b)
		hsep = HSeparator.new()
		hsep.size_flags_horizontal = SIZE_EXPAND_FILL
		hsep.mouse_filter = Control.MOUSE_FILTER_IGNORE
		path_list.add_child(hsep)

func button_pressed(path:String) -> void:
	if Globals.ctrl_pressed and Globals.shift_pressed: OS.set_clipboard(path.get_file())
	elif Globals.ctrl_pressed: pass
	elif Globals.shift_pressed: OS.set_clipboard(path)
	else: OS.set_clipboard(path.get_base_dir())

	# click: copy folder path
	# ctrl-click: open new tab (filtered to the folder)
	# shift-click: copy entire image path
	# ctrl+shift-click: copy file name
	
