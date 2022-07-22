extends MarginContainer

enum file_context_menu { ImportImages, }
enum view_context_menu { FullScreen, ShowThumbnailTooltips }

onready var import_panel:PopupPanel = $ppanel_import
onready var darkened_background:ColorRect = $background/bg_darken

onready var file_dialog:FileDialog = $FileDialog
onready var file_context:PopupMenu = $context_menu/pmenu_file
onready var view_context:PopupMenu = $context_menu/pmenu_view

func _ready() -> void:
	Signals.connect("file_button_pressed", self, "_file_pressed")
	Signals.connect("view_button_pressed", self, "_view_pressed")
	Signals.connect("add_files", self, "show_file_dialog")
	Signals.connect("add_folders", self, "show_file_dialog", [true])
	Signals.connect("new_import_started", self, "hide_popups")
	Signals.connect("new_import_canceled", self, "hide_popups")
	Signals.connect("show_import_menu", self, "show_import_menu")
	Signals.connect("settings_loaded", self, "_settings_loaded")
	
func hide_popups(_import_id:String="", _count:int=0, _import_name:String="") -> void:
	self.hide()
	darkened_background.hide()
	file_context.hide()
	import_panel.hide()

func _file_pressed(position:Vector2) -> void:
	self.show() 
	file_context.popup(Rect2(position, Vector2(1,1)))
	#darkened_background.show()
	#import_panel.popup()

func _view_pressed(position:Vector2) -> void:
	self.show()
	view_context.popup(Rect2(position, Vector2(1,1)))

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

func _on_pmenu_view_context_index_pressed(index:int) -> void:
	if index == view_context_menu.FullScreen:
		var checked:bool = view_context.is_item_checked(index)
		view_context.set_item_checked(index, not checked)
		Globals.settings.use_fullscreen = not checked
		set_fullscreen(not checked)
	elif index == view_context_menu.ShowThumbnailTooltips:
		var checked:bool = view_context.is_item_checked(index)
		view_context.set_item_checked(index, not checked)
		Globals.settings.show_thumbnail_tooltips = not checked
		Signals.emit_signal("toggle_thumbnail_tooltips")
			
func set_fullscreen(on:bool) -> void:
	if on:
		OS.set_window_fullscreen(true)
		OS.set_borderless_window(true)
	else:
		OS.set_window_fullscreen(false)
		OS.set_borderless_window(false)
		OS.set_window_maximized(false)
		OS.set_window_maximized(true) # alternatively, need to resize and center window (and/or store settings before switching to fullscreen)

func _settings_loaded() -> void:
	view_context.set_item_checked(view_context_menu.FullScreen, Globals.settings.use_fullscreen)
	view_context.set_item_checked(view_context_menu.ShowThumbnailTooltips, Globals.settings.show_thumbnail_tooltips)
	set_fullscreen(Globals.settings.use_fullscreen)
