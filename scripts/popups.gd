extends MarginContainer

onready var import_panel:PopupPanel = $ppanel_import
onready var darkened_background:ColorRect = $background/bg_darken

func _ready() -> void:
	Signals.connect("file_button_pressed", self, "_file_pressed")


func _file_pressed() -> void:
	self.show() 
	darkened_background.show()
	import_panel.popup()

func _on_ppanel_import_popup_hide() -> void: 
	self.hide()
	darkened_background.hide()
