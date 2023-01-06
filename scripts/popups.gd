extends MarginContainer

enum file_context_menu { ImportImages, }
enum view_context_menu { FullScreen, ShowThumbnailTooltips, FileButtons=5, TabButtons, 
						 SearchButtons, ThumbnailList, PreviewSection, TabList, TagList }

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
	
func hide_popups(_count:int=0, _import_name:String="") -> void:
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
	var parent_size:Vector2 = self.get_parent().rect_size
	var popup_offset:int = Global.GetOffsetPopupH()
	import_panel.popup(Rect2(popup_offset, popup_offset, parent_size.x-popup_offset*2, parent_size.y-popup_offset*2))

func show_file_dialog(select_folders:bool=false) -> void:
	file_dialog.access = FileDialog.ACCESS_FILESYSTEM
	file_dialog.mode = FileDialog.MODE_OPEN_FILES if not select_folders else FileDialog.MODE_OPEN_DIR #MODE_OPEN_ANY
	file_dialog.set_filters(PoolStringArray(["*.png, *.jpg, *.jpeg ; Image Files"]))
	
	var parent_size:Vector2 = self.get_parent().rect_size
	var popup_offset:int = Global.GetOffsetPopupH()
	file_dialog.popup(Rect2(popup_offset, popup_offset, parent_size.x-popup_offset*2, parent_size.y-popup_offset*2))

func _on_FileDialog_files_selected(paths:Array) -> void: Signals.emit_signal("files_selected", paths)
func _on_FileDialog_dir_selected(dir:String) -> void: Signals.emit_signal("folder_selected", dir)

# move this and others to global input handler, use signals
func _unhandled_input(event:InputEvent) -> void:
	if event is InputEventKey:
		if event.scancode == KEY_1 and event.pressed: 
			var checked:bool = get_set_view_context_checked(view_context_menu.FileButtons)
			Signals.emit_signal("toggle_file_section", checked)
		elif event.scancode == KEY_2 and event.pressed: 
			var checked:bool = get_set_view_context_checked(view_context_menu.TabButtons)
			Signals.emit_signal("toggle_tab_section", checked)
		elif event.scancode == KEY_3 and event.pressed: 
			var checked:bool = get_set_view_context_checked(view_context_menu.SearchButtons)
			Signals.emit_signal("toggle_search_section", checked)
		elif event.scancode == KEY_4 and event.pressed: 
			var checked:bool = get_set_view_context_checked(view_context_menu.ThumbnailList)
			Signals.emit_signal("toggle_thumbnail_section", checked)
		elif event.scancode == KEY_5 and event.pressed: 
			var checked:bool = get_set_view_context_checked(view_context_menu.PreviewSection)
			Signals.emit_signal("toggle_preview_section", checked)
		elif event.scancode == KEY_6 and event.pressed:
			var checked:bool = get_set_view_context_checked(view_context_menu.TabList)
			Signals.emit_signal("toggle_tablist_section", checked)
		elif event.scancode == KEY_7 and event.pressed: 
			var checked:bool = get_set_view_context_checked(view_context_menu.TagList)
			Signals.emit_signal("toggle_tag_section", checked)

func _on_pmenu_view_context_index_pressed(index:int) -> void:
	if index == view_context_menu.FullScreen:
		var checked:bool = get_set_view_context_checked(index)
		#Globals.settings.use_fullscreen = checked
		Global.SetUseFullscreen(checked)
		set_fullscreen(checked)
	elif index == view_context_menu.ShowThumbnailTooltips:
		var checked:bool = get_set_view_context_checked(index)
		#Globals.settings.show_thumbnail_tooltips = checked
		Global.SetShowThumbnailTooltips(checked)
		Signals.emit_signal("toggle_thumbnail_tooltips")
	
	elif index == view_context_menu.FileButtons:
		var checked:bool = get_set_view_context_checked(index)
		Signals.emit_signal("toggle_file_section", checked)
	elif index == view_context_menu.TabButtons:
		var checked:bool = get_set_view_context_checked(index)
		Signals.emit_signal("toggle_tab_section", checked)
	elif index == view_context_menu.SearchButtons:
		var checked:bool = get_set_view_context_checked(index)
		Signals.emit_signal("toggle_search_section", checked)
	elif index == view_context_menu.ThumbnailList:
		var checked:bool = get_set_view_context_checked(index)
		Signals.emit_signal("toggle_thumbnail_section", checked)
	elif index == view_context_menu.PreviewSection:
		var checked:bool = get_set_view_context_checked(index)
		Signals.emit_signal("toggle_preview_section", checked)
	elif index == view_context_menu.TagList:
		var checked:bool = get_set_view_context_checked(index)
		Signals.emit_signal("toggle_tag_section", checked)
	

func get_set_view_context_checked(index:int) -> bool:
	var checked:bool = view_context.is_item_checked(index)
	view_context.set_item_checked(index, not checked)
	return not checked
		
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
	view_context.set_item_checked(view_context_menu.FullScreen, Global.GetUseFullscreen())
	view_context.set_item_checked(view_context_menu.ShowThumbnailTooltips, Global.GetShowThumbnailTooltips())
	set_fullscreen(Global.GetUseFullscreen())
