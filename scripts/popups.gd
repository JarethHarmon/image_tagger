extends MarginContainer

enum file_context_menu { ImportImages, }

onready var import_panel:PopupPanel = $ppanel_import
onready var darkened_background:ColorRect = $background/bg_darken

onready var file_context:PopupMenu = $pmenu_file_context

func _ready() -> void:
	Signals.connect("file_button_pressed", self, "_file_pressed")

func _file_pressed(position:Vector2) -> void:
	self.show() 
	file_context.popup(Rect2(position, Vector2(1,1)))
	#darkened_background.show()
	#import_panel.popup()

func _on_ppanel_import_popup_hide() -> void: 
	self.hide()
	darkened_background.hide()
	
func _on_pmenu_file_context_index_pressed(index:int) -> void:
	if index == file_context_menu.ImportImages: 
		darkened_background.show()
		import_panel.popup()
