extends Control

onready var scroll:ScrollContainer = $margin/scroll
onready var tag_section:VBoxContainer = $margin/scroll/vbox/tags
onready var descriptive_section:VBoxContainer = $margin/scroll/vbox/tags/tag_section/vbox/descriptive

func _ready() -> void:
	Signals.connect("make_descriptive_tags_visible", self, "make_descriptive_tags_visible")
	Signals.connect("toggle_metadata_section", self, "toggle_visibility")

# this should really be made generic and moved into a custom node
func toggle_visibility(_visible:bool) -> void:
	self.visible = _visible
	Globals.toggle_parent_visibility_from_children(self)
	Globals.toggle_parent_visibility_from_children(self.get_parent())

func make_descriptive_tags_visible() -> void:
	scroll.get_v_scrollbar().value = 0
	if tag_section.closed: tag_section.dropdown_pressed()
	if descriptive_section.closed: descriptive_section.dropdown_pressed()
