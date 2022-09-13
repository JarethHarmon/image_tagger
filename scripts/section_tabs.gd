extends Control

onready var tag_list:Control = get_node("../tag_list")
onready var path_list:Control = get_node("../path_list")
onready var import_list:Control = get_node("../import_list")
onready var group_list:Control = get_node("../group_list")
onready var rating_list:Control = get_node("../rating_list")

var controls:Array = []
func _ready() -> void: 
	controls.append_array([tag_list, path_list, import_list, group_list, rating_list])
	Signals.connect("toggle_tablist_section", self, "_toggle_tablist_section")

func _toggle_tablist_section(_visible:bool) -> void: 
	self.visible = _visible
	if Globals.all_siblings_hidden(self): get_parent().hide()
	else: get_parent().show()	

# add some selection highlight colors; actually create the scenes for the other lists

func _on_tags_button_up() -> void:
	if tag_list.visible: return
	for control in controls: control.hide()
	tag_list.show()
	Globals.current_visible_tab_section = tag_list

func _on_paths_button_up() -> void:
	if path_list.visible: return
	for control in controls: control.hide()
	path_list.show()
	Globals.current_visible_tab_section = path_list

func _on_imports_button_up() -> void:
	if import_list.visible: return
	for control in controls: control.hide()
	import_list.show()
	Globals.current_visible_tab_section = import_list

func _on_groups_button_up() -> void:
	if group_list.visible: return
	for control in controls: control.hide()
	group_list.show()
	Globals.current_visible_tab_section = group_list

func _on_ratings_button_up() -> void:
	if rating_list.visible: return
	for control in controls: control.hide()
	rating_list.show()
	Globals.current_visible_tab_section = rating_list
