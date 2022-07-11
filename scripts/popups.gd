extends MarginContainer

enum file_context_menu { ImportImages, }

onready var import_panel:PopupPanel = $ppanel_import
onready var darkened_background:ColorRect = $background/bg_darken

onready var file_dialog:FileDialog = $FileDialog
onready var file_context:PopupMenu = $pmenu_file_context

func _ready() -> void:
	Signals.connect("file_button_pressed", self, "_file_pressed")
	Signals.connect("add_files", self, "show_file_dialog")
	Signals.connect("add_folders", self, "show_file_dialog", [true])
	Signals.connect("new_import_started", self, "import_started")
	Signals.connect("show_import_menu", self, "show_import_menu")
	
func import_started(_import_id:String, _count:int) -> void:
	self.hide()
	darkened_background.hide()
	file_context.hide()
	import_panel.hide()

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
		show_import_menu()

func show_import_menu() -> void:
	darkened_background.show()
	import_panel.popup()

func show_file_dialog(select_folders:bool=false) -> void:
	#ImageScanner.OpenFileBrowser() # looks bad, does not allow selecting folders currently, bad ux
	file_dialog.access = FileDialog.ACCESS_FILESYSTEM
	file_dialog.mode = FileDialog.MODE_OPEN_FILES if not select_folders else FileDialog.MODE_OPEN_DIR #MODE_OPEN_ANY
	file_dialog.set_filters(PoolStringArray(["*.png, *.jpg, *.jpeg ; Image Files"]))
	
	file_dialog.popup()

func _on_FileDialog_files_selected(paths:Array) -> void: Signals.emit_signal("files_selected", paths)
func _on_FileDialog_dir_selected(dir:String) -> void: Signals.emit_signal("folder_selected", dir)

