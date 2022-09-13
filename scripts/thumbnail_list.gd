extends Control

func _ready() -> void:
	Signals.connect("toggle_thumbnail_section", self, "_toggle_thumbnail_section")

func _toggle_thumbnail_section(_visible:bool) -> void: 
	self.visible = _visible
	if Globals.all_siblings_hidden(self): get_parent().hide()
	else: get_parent().show()
	if Globals.all_siblings_hidden(get_parent()): get_parent().get_parent().hide()
	else: get_parent().get_parent().show()
