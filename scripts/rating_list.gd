extends Control


func _ready():
	Signals.connect("toggle_tag_section", self, "_toggle_tag_section")

func _toggle_tag_section(_visible:bool) -> void: 
	if not Globals.current_visible_tab_section == self: return
	self.visible = _visible
	if Globals.all_siblings_hidden(self): get_parent().hide()
	else: get_parent().show()
