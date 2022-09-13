extends Control


func _ready():
	Signals.connect("toggle_tag_section", self, "_toggle_tag_section")

func _toggle_tag_section(_visible:bool) -> void: 
	if not Globals.current_visible_tab_section == self: return
	self.visible = _visible
	Globals.toggle_parent_visibility_from_children(self)
	Globals.toggle_parent_visibility_from_children(self.get_parent())
