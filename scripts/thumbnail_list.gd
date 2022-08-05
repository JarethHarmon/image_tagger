extends Control

func _ready() -> void:
	Signals.connect("toggle_thumbnail_section", self, "_toggle_thumbnail_section")

func _toggle_thumbnail_section(_visible:bool) -> void: self.visible = _visible
