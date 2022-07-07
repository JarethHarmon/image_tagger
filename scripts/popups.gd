extends MarginContainer

onready var import_panel:PopupPanel = $ppanel_import


func _ready() -> void:
	Signals.connect("file_button_pressed", self, "_file_pressed")


func _file_pressed() -> void: import_panel.popup()
