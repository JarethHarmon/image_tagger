extends Control

onready var vbox:VBoxContainer = $margin/vbox

func _ready() -> void:
	Signals.connect("create_path_buttons", self, "_create_path_buttons")
	Signals.connect("toggle_tag_section", self, "_toggle_tag_section")

func _toggle_tag_section(_visible:bool) -> void: 
	if not Globals.current_visible_tab_section == self: return
	self.visible = _visible

func _create_path_buttons(sha256:String, paths:Array) -> void:
	for child in vbox.get_children(): child.queue_free()
	for path in paths:
		var b:Button = Button.new()
		b.text = "  " + path + "  "
		b.size_flags_horizontal = SIZE_EXPAND_FILL
		vbox.add_child(b)
