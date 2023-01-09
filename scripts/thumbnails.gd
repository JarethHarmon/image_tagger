extends ItemList

# one issue with the current approach to page history is that if the page has changed by a method not included
# in the hash (e.g. additional/removed tags on images when sorting by tag count) ; the page will not update, and 
# cannot be updated for the current query settings

# one option is to just allow manual refreshes with F5 and the search button; though it requires a manual action
# for each page that was previously loaded into memory for the tab with changed images

# another option is to always re-query pages sorted by tag count, or any similar concepts in the future (like ratings)

# another option is to only re-query when it needs to, but that is not at all easy to determine 
# 	(unless I am just too tired and missing something obvious)


# need to fix these references
onready var thumb_size:HSlider = self.get_parent().get_node("sort_buttons/thumbnail_size")
onready var thumb_size_entry:SpinBox = self.get_parent().get_node("sort_buttons/thumbnail_size_entry")
onready var th:Mutex = Mutex.new()			# mutex for interacting with thumb_history

var tab_history:Dictionary = {} 			# { tab_id:{ "scroll":scroll_percentage , "page":page } }
var thumb_history:Dictionary = {}			# { image_hash:ImageTexture }
var current_hashes:Array = []
var selected_items:Dictionary = {}

var called_already:bool = false
var ctrl_pressed:bool = false
var shift_pressed:bool = false

var selected_item:int = 0
var last_selected_item:int = 0
var last_index:int = 0
var curr_page_number:int = 1

func _curr_page_changed(new_page:int) -> void: 
	var tab_id:String = Globals.current_tab_id
	if tab_history.has(tab_id):
		tab_history[tab_id].page = new_page
	else:
		tab_history[tab_id] = {"scroll":0.0, "page":new_page}
	curr_page_number = new_page
	Signals.emit_signal("change_page", new_page)

func _ready() -> void:
	Signals.connect("page_label_ready", self, "_page_label_ready")
	Signals.connect("page_changed", self, "_curr_page_changed")
	Signals.connect("select_all_pressed", self, "select_all_items")
	Signals.connect("deselect_all_pressed", self, "deselect_all")
	Signals.connect("toggle_thumbnail_tooltips", self, "_toggle_thumbnail_tooltips")
	self.get_v_scroll().connect("scrolling", self, "_scrolling")
	self.get_v_scroll().connect("value_changed", self, "_scrolling")	

#-----------------------------------------------------------------------#
#					             OTHER									#
#-----------------------------------------------------------------------#
func _create_tooltip(image_hash:String, dict:Dictionary, index:int) -> String:
	var tooltip:String = "index: " + String(index+1)
	if dict.empty() or dict == null: return tooltip
	tooltip += "\nsize: " + ("-1" if dict.size == "" else String.humanize_size(dict.size.to_int()))
	tooltip += "\nname: " + dict.image_name
	tooltip += "\ncreation time: " + dict.creation_time 
	tooltip += "\nsha256 hash: " + image_hash
	tooltip += "\ndifference hash: " + dict.diff_hash
	tooltip += "\ncolor hash: " + String(dict.color_hash)
	return tooltip
	
func _on_thumbnails_multi_selected(index:int, selected:bool) -> void:
	selected_item = index
	last_index = index
	if called_already: return
	called_already = true
	call_deferred("select_items")

func deselect_all() -> void:
	selected_items.clear()
	self.unselect_all()

func uncolor_all() -> void: 
	for idx in selected_items: 
		self.set_item_custom_bg_color(idx, Color.transparent)
		
func color_all() -> void:
	for idx in selected_items: 
		self.set_item_custom_bg_color(idx, Color.red) 

func select_items() -> void:
	selected_items.clear()
	var arr_index:Array = self.get_selected_items()
	if arr_index.size() == 0: return
	
	for i in arr_index.size():
		selected_items[arr_index[i]] = current_hashes[arr_index[i]]
	
	var image_hash:String = current_hashes[last_index]
	MetadataManager.LoadCurrentImageInfo(image_hash)
	var paths:Array = MetadataManager.GetCurrentPaths()
	var imports:Array = MetadataManager.GetCurrentImports()
	Signals.emit_signal("load_image_tags", image_hash, selected_items)
	Signals.emit_signal("create_path_buttons", image_hash, paths)
	if imports != null: Signals.emit_signal("create_import_buttons", image_hash, imports)
	
	if not paths.empty():
		var f:File = File.new()
		var found:bool = false
		for path in paths:
			if f.file_exists(path):
				Signals.emit_signal("load_full_image", image_hash, path)
				found = true
				break
		if not found: Signals.emit_signal("load_full_image", image_hash, "", false)
	called_already = false

func select_all_items() -> void: 
	selected_items.clear()
	var tab_id:String = Globals.current_tab_id
	for i in current_hashes.size(): 
		selected_items[i] = current_hashes[i]
		self.select(i, false)
	Signals.emit_signal("all_selected_items", selected_items)
	
# need to add proper support for shift+up/down arrow key
# (ie need to select/deselect everything between the last_selected_item and the selected_item 
# (based on whether selected_item is in selected_items already)
func _unhandled_input(event:InputEvent) -> void:
	if Input.is_action_pressed("ctrl"): ctrl_pressed = true
	if Input.is_action_pressed("shift"): shift_pressed = true
	
	if event is InputEventKey:
		if Input.is_action_just_pressed("arrow_left"): 
			selected_item = int(max(0, selected_item-1)) 
			if ctrl_pressed or shift_pressed: self.select(selected_item, false)
			else: self.select(selected_item)
			_on_thumbnails_multi_selected(selected_item, false)
			scroll(false)
		elif Input.is_action_just_pressed("arrow_right"):
			selected_item = int(min(get_item_count()-1, selected_item+1))
			if ctrl_pressed or shift_pressed: self.select(selected_item, false)
			else: self.select(selected_item)
			_on_thumbnails_multi_selected(selected_item, false)
			scroll(true)
		elif Input.is_action_just_pressed("arrow_up"):
			selected_item = int(max(0, selected_item-get_num_columns()))
			if ctrl_pressed or shift_pressed: self.select(selected_item, false)
			else: self.select(selected_item)
			_on_thumbnails_multi_selected(selected_item, false)
			scroll(false)
		elif Input.is_action_just_pressed("arrow_down"):
			selected_item = int(min(get_item_count()-1, selected_item+get_num_columns()))
			if ctrl_pressed or shift_pressed: self.select(selected_item, false)
			else: self.select(selected_item)
			_on_thumbnails_multi_selected(selected_item, false)
			scroll(true)
			
	ctrl_pressed = false
	shift_pressed = false
	
func scroll(down:bool=true) -> void:
	if self.get_item_count() <= 1: return
	var vscroll:VScrollBar = self.get_v_scroll()
	var current_columns:int = self.get_num_columns()
	var num_rows:int = int(ceil(float(self.get_item_count())/current_columns))
	var current_row:int = int(floor(float(selected_item)/current_columns))
	var max_value:float = vscroll.max_value
	var has_text:bool = not self.get_item_text(0) == ""
	var value:float = 0.0
	
	if down:
		if current_row > 1: value = (current_row-1) * (ceil(max_value/num_rows) - (2 if has_text else 1))
	else:
		if current_row < num_rows-2: value = (current_row-1) * (ceil(max_value/num_rows) - (2 if has_text else 1))
		else: value = vscroll.max_value
	vscroll.set_value(value)

func _scrolling(value:float=0.0) -> void:
	return
	tab_history[Globals.current_tab_id].scroll = value / self.get_v_scroll().max_value
	 
func color_selection(index:int, selected:bool) -> void:
	if self.get_item_count()-1 < index: return
	if selected:
		self.set_item_custom_bg_color(index, Color.lime)
		# fg color too
	else:
		self.set_item_custom_bg_color(index, Color.transparent) # not sure what default is 

func get_num_columns() -> int:
	var fixed_x:int = int(self.fixed_icon_size.x)
	var hsep:int = 3 # copy-paste from the last theme override affecting the itemlist (if changeable in your program then set it with that)
	#var hsep:int = self.get_constant("hseparation")
	var sep_sides:int = int(floor(float(hsep)/2)) # sides are half as large as in-between items, rounded down (this might actually be a constant 1)
		# items = 3
		# hsep = 1: total_sep = 1/2 + 1 + 1 + 1/2	 = 2
		# hsep = 2: total_sep = 1 + 2 + 2 + 1		 = 6
		# hsep = 3: total_sep = 3/2 + 3 + 3 + 3/2	 = 8
	var size_x:int = int(self.rect_size.x)
	var scroll_x:int = int(self.get_v_scroll().rect_size.x)
	
	var result:int = 1
	for i in range(1, self.max_columns):
		var a:int = size_x-scroll_x-sep_sides-hsep # extra hsep for the vscroll (I think)
		var b:int = (i * fixed_x + (i-1)*hsep)-1 # -1 for the numbers where it fits perfectly (ie a/b perfectly returns 1)
		if int(floor(float(a)/b)) == 0: return result
		result = i
	return 1

func _on_thumbnail_size_value_changed(value:int) -> void:
	self.fixed_icon_size = Vector2(value, value)
	self.fixed_column_width = value
	thumb_size_entry.value = value
	Global.SetThumbnailWidth(value)
	
func _on_thumbnail_size_entry_value_changed(value:int) -> void:
	self.fixed_icon_size = Vector2(value, value)
	self.fixed_column_width = value
	thumb_size.value = value
	Global.SetThumbnailWidth(value)

func _toggle_thumbnail_tooltips() -> void:
	var show_tooltips:bool = Global.GetShowThumbnailTooltips()
	if show_tooltips:
		for idx in self.get_item_count():
			var image_hash:String = current_hashes[idx]
			th.lock()
			if thumb_history.has(image_hash):
				var dict:Dictionary = thumb_history[image_hash]
				th.unlock()
				self.set_item_tooltip(idx, _create_tooltip(image_hash, dict, idx))
			else: th.unlock()
	else:
		for idx in self.get_item_count():
			self.set_item_tooltip(idx, "")
			
func _on_thumbnails_item_rmb_selected(index:int, _at_position:Vector2) -> void:
	# actual code should show a context menu
	var image_hash:String = current_hashes[index]
	Signals.emit_signal("create_similarity_tab", image_hash)
