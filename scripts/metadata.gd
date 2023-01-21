extends Control

onready var scroll:ScrollContainer = $margin/scroll
onready var tag_section:VBoxContainer = $margin/scroll/vbox/tags
onready var descriptive_section:VBoxContainer = $margin/scroll/vbox/tags/tag_section/vbox/descriptive

func _ready() -> void:
	Signals.connect("make_descriptive_tags_visible", self, "make_descriptive_tags_visible")

func make_descriptive_tags_visible() -> void:
	scroll.get_v_scrollbar().value = 0
	if tag_section.closed: tag_section.dropdown_pressed()
	if descriptive_section.closed: descriptive_section.dropdown_pressed()
