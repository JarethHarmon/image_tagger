extends Control


func _ready():
	Signals.connect("toggle_search_section", self, "_toggle_section")
	Signals.connect("toggle_preview_section", self, "_toggle_section")
	Signals.connect("toggle_tablist_section", self, "_toggle_section")
	Signals.connect("toggle_tag_section", self, "_toggle_section")
	
func _toggle_section(_visible:bool) -> void:
	Globals.toggle_parent_visibility_from_children(self)
	Globals.toggle_parent_visibility_from_children(self.get_parent())

