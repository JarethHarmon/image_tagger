extends Control

onready var sibling:MarginContainer = get_parent().get_node("margin")

func _ready() -> void:
	Signals.connect("set_metadata_section_expand", self, "set_child")

func set_child(section:Control) -> void:
	if get_child_count() > 0: remove_child(get_child(0))
	if section != null: 
		add_child(section)
		sibling.hide()
	else: sibling.show()
