extends ItemList

# constants
const icon_buffering:StreamTexture = preload("res://assets/buffer-01.png")
const icon_broken:StreamTexture = preload("res://assets/icon-broken.png") # want to remove this eventually

# nodes
var page_label:Label

# signal connections
func _page_label_ready(node_path:NodePath) -> void: page_label = get_node(node_path) 

# instance variables
onready var sc:Mutex = Mutex.new()		# mutex for interacting with scene
onready var ii:Mutex = Mutex.new()		# mutex for interacting with item_index
onready var tq:Mutex = Mutex.new()		# mutex for interacting with thumb_queue
onready var th:Mutex = Mutex.new()		# mutex for interacting with thumb_history

# data structures
# change to a { {}:{} } data structure, need to include search settings and image_hashes in values
#	search settings should include tag arrays, import_id, group_id, image_hash (for similarity sorting) 
#	as is relevant
# all tabs will use this system; new tabs that are opened (like similarity sorting) will generate a new import_id
# and upload themselves to the database
var tab_settings:Dictionary = {}
var page_history:Dictionary = {}		# [page_number, load_id, type_id]:[image_hashes] :: stores last M pages of image_hashes
var thumb_history:Dictionary = {}		# image_hash:ImageTexture :: stores last P loaded thumbnails  ->> image_hash:{texture&metadata}
var image_queue:Array = []				# [image_hashes] :: fifo queue of last N loaded full image hashes  
var page_queue:Array = []				# [[page_number, load_id, type_id]] :: fifo queue of last M pages
var thumb_queue:Array = []				# [image_hashes] :: fifo queue of the thumbnails waiting to be loaded for the current page
var load_threads:Array = []				# array of threads for loading thumbnails

# variables
var item_index:int = 0					# current index in the item_list
var curr_page_number:int = 1			# the viewed page for the current query
var curr_page_image_count:int = 0		# the number of thumbnails for the current page
var total_image_count:int = 0			# the total number of images for the the current group (all/import_group/image_group)
var queried_page_count:int = 1			# the number of pages for the current query (queried_image_count/images_per_page)
var queried_image_count:int = 0			# the number of images returned by the current query
var database_offset:int = 0				# the offset in the database for the current query ((current_page_number-1)*images_per_page)

var last_query_settings:Array = []		# the settings used for the last query (used to avoid counting query results multiple times)

var starting_load_process:bool = false	# whether a page is beginning the load process
var stopping_load_process:bool = false	# whether a page is attempting to stop loading

# when user loads a page (including by clicking a group button) it should take the tID, lID and p# and check if present in page history first
# when a page is removed from page_history, iterate over its hash array and remove those from loaded_thumbnails

# signal connections
func _curr_page_changed(new_page:int) -> void: 
	# should set it based on a (new?) history data structure that saves the last viewed page for each importId (or 1 if it does not exist)
	curr_page_number = new_page
	Signals.emit_signal("change_page")

# initialization
func _ready() -> void:
	Signals.connect("page_label_ready", self, "_page_label_ready")
	Signals.connect("page_changed", self, "_curr_page_changed")
	Signals.connect("search_pressed", self, "prepare_query")
	Signals.connect("select_all_pressed", self, "select_all_items")
	Signals.connect("deselect_all_pressed", self, "deselect_all")
	Signals.connect("toggle_thumbnail_tooltips", self, "_toggle_thumbnail_tooltips")

func _prepare_query(include_tags:Array=[], exclude_tags:Array=[]) -> void:
	# include = [ [ A,B ] , [ C,D ] , [ E ] ]
	#		means that an image must include one of : (A and B) or (C and D) or (E)
	# this will be the default, current prepare_query() should call this and construct tags like this instead
	# then I need to design UI to support these mini tag groupings
	pass
	
func prepare_query(tags_all:Array=[], tags_any:Array=[], tags_none:Array = [], new_query:bool=true) -> void: 
	if new_query:
		curr_page_number = 1
		Signals.emit_signal("page_changed", curr_page_number)
		queried_page_count = 0
		total_image_count = 0
	start_query(Globals.current_tab_id, tags_all, tags_any, tags_none)

# need to decide if I should save query settings to history (ie attached on a per-import basis) or if I should apply them globally to any selected import button
func start_query(tab_id:String, tags_all:Array=[], tags_any:Array=[], tags_none:Array=[]) -> void:
	if starting_load_process: return
	if tab_id == "": return
	starting_load_process = true
	stop_threads()
  # temp variables
	var hash_arr:Array = []
	var images_per_page:int = Globals.settings.images_per_page
	var num_threads:int = Globals.settings.load_threads
	var current_sort:int = Globals.settings.current_sort
	var current_order:int = Globals.settings.current_order
	var current_page:Array = [curr_page_number, tab_id]
	var thumbnail_path:String = Globals.settings.thumbnail_path
	var temp_query_settings = [tab_id, tags_all, tags_any, tags_none]
	
  # calculate offset
	database_offset = (curr_page_number-1) * images_per_page

  # query database
	var count_results:bool = not(temp_query_settings == last_query_settings)
	last_query_settings = temp_query_settings
	
	# total_count is only used for updating import button, so attach that information to the import buttons themselves, have them query it when the user clicks one
	hash_arr = Database.QueryDatabase(tab_id, database_offset, images_per_page, tags_all, tags_any, tags_none, current_sort, current_order, count_results)
	queried_image_count = Database.GetLastQueriedCount() # just returns a private int, will be updated by the QueryDatabase() call if count_results is true (ie when query settings have changed)
	queried_page_count = ceil(float(queried_image_count)/float(images_per_page)) as int
	Signals.emit_signal("max_pages_changed", queried_page_count)
	# display time taken for query
	#var text:String = String(queried_image_count) + " : %1.3f ms" % [float(OS.get_ticks_usec()-time)/1000.0] 
	#get_node("/root/main/Label").text = text
	
  # remove from page history
	
	if page_history.size() > Globals.settings.pages_to_store:
		var page_to_remove:Array = page_queue.pop_front()
		var thumbnails_to_unload:Array = page_history[page_to_remove]
		th.lock()
		for thumbnail in thumbnails_to_unload: thumb_history.erase(thumbnail)
		th.unlock()
		page_history.erase(page_to_remove)

  # add to page history
	if not page_history.has(current_page): 
		page_queue.push_back(current_page)
	page_history[current_page] = hash_arr
	
  # set page image count
	curr_page_image_count = hash_arr.size()
	
	self.call_deferred("_threadsafe_clear", tab_id, curr_page_number, curr_page_image_count, queried_page_count, num_threads) 
	
# threading
func stop_threads() -> void:
	stopping_load_process = true
	for t in load_threads.size():
		stop_thread(t)

func stop_thread(thread_id:int) -> void:
	if load_threads[thread_id].is_active() or load_threads[thread_id].is_alive():
		load_threads[thread_id].wait_to_finish()

func _threadsafe_clear(tab_id:String, page_number:int, image_count:int, page_count:int, num_threads:int) -> void:
	#starting_load_process = false
	sc.lock()
	if self.get_item_count() > 0:
		yield(get_tree(), "idle_frame")
		self.clear()
		yield(get_tree(), "idle_frame")
		yield(get_tree(), "idle_frame")
	for i in image_count:
		self.add_item("") # self.add_item(page_history[[page_number, import_id]][i]) # 
		self.set_item_icon(i, icon_buffering)
	# set page label text ( curr_page/total_pages )
	sc.unlock()
	prepare_thumbnail_loading(tab_id, page_number, num_threads)

func prepare_thumbnail_loading(tab_id:String, page_number:int, num_threads:int) -> void:
	load_threads.clear()
	for i in num_threads: 
		load_threads.append(Thread.new())
	
	ii.lock() ; item_index = 0 ; ii.unlock()
	tq.lock()
	thumb_queue.clear()
	thumb_queue = page_history[[page_number, tab_id]].duplicate()
	tq.unlock()
	
	stopping_load_process = false
	for t in load_threads.size():
		if not load_threads[t].is_active():
			load_threads[t].start(self, "_thread", t)
	starting_load_process = false # putting this here instead of line 140 is more stable, but results in slower page changing
	# need to try and improve page changing speed in general
	
func _thread(thread_id:int) -> void:
	while not stopping_load_process:
		tq.lock()
		if thumb_queue.empty():
			tq.unlock()
			break
		else:
			ii.lock()
			var image_hash:String = thumb_queue.pop_front()
			var index:int = item_index
			item_index += 1
			ii.unlock()
			tq.unlock()
			load_thumbnail(image_hash, index)
		OS.delay_msec(50)
	call_deferred("stop_thread", thread_id)

# thumbnail loading
func load_thumbnail(image_hash:String, index:int) -> void:
	th.lock()
	if thumb_history.has(image_hash):
		th.unlock()
		if stopping_load_process: return
		_threadsafe_set_icon(image_hash, index)
	else:
		th.unlock()
		_set_metadata(image_hash)
		
		var f:File = File.new()
		var p:String = Globals.settings.thumbnail_path.plus_file(image_hash.substr(0, 2)).plus_file(image_hash) + ".thumb"
		var e:int = f.open(p, File.READ)
		
		if stopping_load_process: return
		if e != OK: 
			_threadsafe_set_icon(image_hash, index, true)
			return
		
		if stopping_load_process: return
		var file_type:int = Database.GetFileType(image_hash)
		
		if file_type == Globals.ImageType.FAIL:
			_threadsafe_set_icon(image_hash, index, true)
			return
		
		if stopping_load_process: return
		var i:Image = Image.new()
		var b:PoolByteArray = f.get_buffer(f.get_len())
		if file_type == Globals.ImageType.PNG: 
			e = i.load_png_from_buffer(b)
		elif file_type == Globals.ImageType.JPG: 
			e = i.load_jpg_from_buffer(b)
		else:
			_threadsafe_set_icon(image_hash, index, true)
			return
		if e != OK: print_debug(e, " :: ", image_hash + ".thumb")
		
		if stopping_load_process: return
		var it:ImageTexture = ImageTexture.new()
		it.create_from_image(i, 0) # FLAGS # 4
		it.set_meta("image_hash", image_hash)
		
		th.lock() ; thumb_history[image_hash]["texture"] = it ; th.unlock()
		
		if stopping_load_process: return
		_threadsafe_set_icon(image_hash, index)

func _threadsafe_set_icon(image_hash:String, index:int, failed:bool=false) -> void:
	var im_tex:Texture
	var dict:Dictionary = {}
	if failed: im_tex = icon_broken
	else:
		th.lock()
		dict = thumb_history[image_hash]
		im_tex = dict.texture
		th.unlock()
	if stopping_load_process: return
	sc.lock()
	self.set_item_icon(index, im_tex)
	if Globals.settings.show_thumbnail_tooltips:
		var tooltip:String = _create_tooltip(image_hash, dict, index)
		set_item_tooltip(index, tooltip)
	#set_item_tooltip(index, "sha256: " + image_hash + "\ndifference hash: " + diff_hash + "\ncolor hash: " + color_hash as String + +  + "\npaths: " + String(paths))
	#set_item_text(index, String(index+1))
	# If I include text options, will need to edit scroll() to account for the increased vertical height
	# would also need to limit it to one line of text, and I don't believe I was ever successful in calculating 
	# the correct offset in the past
	sc.unlock()

func _create_tooltip(image_hash:String, dict:Dictionary, index:int) -> String:
	var tooltip:String = "index: " + String(index+1)
	tooltip += "\nsize: " + ("-1" if dict.size == "" else String.humanize_size(dict.size.to_int()))
	tooltip += "\ncreation time: " + dict.creation_time 
	tooltip += "\nsha256 hash: " + image_hash
	tooltip += "\ndifference hash: " + dict.diff_hash
	tooltip += "\ncolor hash: " + String(dict.color_hash)
	return tooltip

# might need to be made threadsafe (or else change dictHashes to a ConcurrentDictionary)
func _set_metadata(image_hash:String) -> void:
	var size:String = Database.GetFileSize(image_hash)
	var diff_hash:String = Database.GetDiffHash(image_hash)
	var color_hash:Array = Database.GetColorHash(image_hash)
	var creation_time:String = Database.GetCreationTime(image_hash)
	var paths:Array = Database.GetPaths(image_hash)# (create paths section again)
	th.lock()
	# dimensions
	var dict:Dictionary = {
		"texture" : null,
		"size" : size,
		"diff_hash" : diff_hash,
		"color_hash" : color_hash,
		"creation_time" : creation_time,
		"paths" : paths
	}
	thumb_history[image_hash] = dict
	th.unlock()
	
var selected_items:Dictionary = {}
var last_index:int = 0
var called_already:bool = false
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
	#uncolor_all()
	selected_items.clear()
	var arr_index:Array = self.get_selected_items()
	if arr_index.size() == 0: return
	for i in arr_index.size():
		selected_items[arr_index[i]] = page_history[[curr_page_number, Globals.current_tab_id]][arr_index[i]]
	#color_all()
	
	var image_hash:String = page_history[[curr_page_number, Globals.current_tab_id]][last_index]
	var paths:Array = Database.GetPaths(image_hash)
	if not paths.empty():
		var f:File = File.new()
		for path in paths:
			if f.file_exists(path):
				Signals.emit_signal("load_full_image", image_hash, path)
				Signals.emit_signal("load_image_tags", image_hash, selected_items)
				Signals.emit_signal("create_path_buttons", image_hash, paths)
				break
	called_already = false

func select_all_items() -> void: 
	selected_items.clear()
	var tab_id:String = Globals.current_tab_id
	for i in curr_page_image_count: 
		selected_items[i] = page_history[[curr_page_number, tab_id]][i]
		self.select(i, false)

var ctrl_pressed:bool = false
var shift_pressed:bool = false

# need to add proper support for shift+up/down arrow key
# (ie need to select/deselect everything between the last_selected_item and the selected_item 
# (based on whether selected_item is in selected_items already)
var selected_item:int = 0
var last_selected_item:int = 0

func _unhandled_input(event:InputEvent) -> void:
	if Input.is_action_pressed("ctrl"): ctrl_pressed = true
	if Input.is_action_pressed("shift"): shift_pressed = true
	
	if event is InputEventKey:
		if Input.is_action_just_pressed("arrow_left"): 
			selected_item = max(0, selected_item-1) 
			if ctrl_pressed or shift_pressed: self.select(selected_item, false)
			else: self.select(selected_item)
			_on_thumbnails_multi_selected(selected_item, false)
			scroll(false)
		elif Input.is_action_just_pressed("arrow_right"):
			selected_item = min(get_item_count()-1, selected_item+1)
			if ctrl_pressed or shift_pressed: self.select(selected_item, false)
			else: self.select(selected_item)
			_on_thumbnails_multi_selected(selected_item, false)
			scroll(true)
		elif Input.is_action_just_pressed("arrow_up"):
			selected_item = max(0, selected_item-get_num_columns())
			if ctrl_pressed or shift_pressed: self.select(selected_item, false)
			else: self.select(selected_item)
			_on_thumbnails_multi_selected(selected_item, false)
			scroll(false)
		elif Input.is_action_just_pressed("arrow_down"):
			selected_item = min(get_item_count()-1, selected_item+get_num_columns())
			if ctrl_pressed or shift_pressed: self.select(selected_item, false)
			else: self.select(selected_item)
			_on_thumbnails_multi_selected(selected_item, false)
			scroll(true)
			
	ctrl_pressed = false
	shift_pressed = false
	
func scroll(down:bool=true) -> void:
	var fixed_y:int = self.fixed_icon_size.y
	var vsep:int = 3 
	var linesep:int = 3
	var sidesep:int = vsep/2
	var vscroll:VScrollBar = self.get_v_scroll()
	var current_columns:int = self.get_num_columns()
	var num_rows:int = ceil(self.get_item_count()/current_columns)
	var current_row:int = selected_item/current_columns

	if down:
		if current_row > 1: vscroll.set_value(((current_row-1) * (fixed_y+linesep+vsep)) + sidesep)
		else: vscroll.set_value(0)
	else:
		if current_row < num_rows-2: vscroll.set_value(((current_row-1) * (fixed_y+linesep+vsep)) + sidesep)
		else: vscroll.set_value(vscroll.max_value)

func color_selection(index:int, selected:bool) -> void:
	if self.get_item_count()-1 < index: return
	if selected:
		self.set_item_custom_bg_color(index, Color.lime)
		# fg color too
	else:
		self.set_item_custom_bg_color(index, Color.transparent) # not sure what default is 

func get_num_columns() -> int:
	var fixed_x:int = self.fixed_icon_size.x
	var hsep:int = 3 # copy-paste from the last theme override affecting the itemlist (if changeable in your program then set it with that)
	var sep_sides:int = hsep/2 # sides are half as large as in-between items, rounded down
		# items = 3
		# hsep = 1: total_sep = 1/2 + 1 + 1 + 1/2	 = 2
		# hsep = 2: total_sep = 1 + 2 + 2 + 1		 = 6
		# hsep = 3: total_sep = 3/2 + 3 + 3 + 3/2	 = 8
	var size_x:int = self.rect_size.x
	var scroll_x:int = self.get_v_scroll().rect_size.x
	
	var result:int = 1
	for i in range(1, self.max_columns):
		var a:int = size_x-scroll_x-sep_sides-hsep # extra hsep for the vscroll (I think)
		var b:int = (i * fixed_x + (i-1)*hsep)-1 # -1 for the numbers where it fits perfectly (ie a/b perfectly returns 1)
		if a / b == 0: return result
		result = i
	return 1

onready var thumb_size:HSlider = self.get_parent().get_node("sort_buttons/thumbnail_size")
onready var thumb_size_entry:SpinBox = self.get_parent().get_node("sort_buttons/thumbnail_size_entry")

func _on_thumbnail_size_value_changed(value:int) -> void:
	self.fixed_icon_size = Vector2(value, value)
	self.fixed_column_width = value
	thumb_size_entry.value = value
	
func _on_thumbnail_size_entry_value_changed(value:int) -> void:
	self.fixed_icon_size = Vector2(value, value)
	self.fixed_column_width = value
	thumb_size.value = value

func _toggle_thumbnail_tooltips() -> void:
	var show_tooltips:bool = Globals.settings.show_thumbnail_tooltips
	if show_tooltips:
		for idx in self.get_item_count():
			var image_hash:String = page_history[[curr_page_number, Globals.current_tab_id]][idx]
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
	var image_hash:String = page_history[[curr_page_number, Globals.current_tab_id]][index]
	Signals.emit_signal("create_similarity_tab", image_hash)

