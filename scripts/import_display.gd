extends Control

onready var vbox:VBoxContainer = $margin/vbox

func _ready() -> void:
	Signals.connect("create_import_buttons", self, "_create_import_buttons")
	Signals.connect("toggle_tag_section", self, "_toggle_tag_section")

func _toggle_tag_section(_visible:bool) -> void: 
	if not Globals.current_visible_tab_section == self: return
	self.visible = _visible
	Globals.toggle_parent_visibility_from_children(self)
	Globals.toggle_parent_visibility_from_children(self.get_parent())

func _create_import_buttons(sha256:String, imports:Array) -> void:
	for child in vbox.get_children(): child.queue_free()
	for import in imports:
		var b:Button = Button.new()
		b.text = "  " + import + "  "
		b.size_flags_horizontal = SIZE_EXPAND_FILL
		vbox.add_child(b)
