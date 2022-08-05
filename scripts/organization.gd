extends Control

func _ready() -> void:
	Signals.connect("toggle_search_section", self, "_toggle_search_section")

func _toggle_search_section(_visible:bool) -> void: self.visible = _visible
