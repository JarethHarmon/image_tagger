extends Control

func _ready() -> void:
	Signals.connect("toggle_search_section", self, "_toggle_search_section")

func _toggle_search_section(_visible:bool) -> void: 
	self.visible = _visible
	Globals.toggle_parent_visibility_from_children(self)
	Globals.toggle_parent_visibility_from_children(self.get_parent())
