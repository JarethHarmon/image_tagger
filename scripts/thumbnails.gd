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
var image_history:Dictionary = {}		# image_hash:ImageTexture :: stores last N loaded full images
var page_history:Dictionary = {}		# [page_number, load_id, type_id]:[image_hashes] :: stores last M pages of image_hashes
var thumb_history:Dictionary = {}		# image_hash:ImageTexture :: stores last P loaded thumbnails
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
	curr_page_number = new_page
	Signals.emit_signal("change_page")

# initialization
func _ready() -> void:
	Signals.connect("page_label_ready", self, "_page_label_ready")
	Signals.connect("page_changed", self, "_curr_page_changed")
	Signals.connect("search_pressed", self, "prepare_query")


func _prepare_query(include_tags:Array=[], exclude_tags:Array=[]) -> void:
	# include = [ [ A,B ] , [ C,D ] , [ E ] ]
	#		means that an image must include one of : (A and B) or (C and D) or (E)
	# this will be the default, current prepare_query() should call this and construct tags like this instead
	# then I need to design UI to support these mini tag groupings
	pass
	
func prepare_query(tags_all:Array=[], tags_any:Array=[], tags_none:Array = [], new_query:bool=true) -> void: 
	if new_query:
		curr_page_number = 1
		queried_page_count = 0
		total_image_count = 0
	start_query(Globals.current_import_id, Globals.current_group_id, tags_all, tags_any, tags_none)

# need to decide if I should save query settings to history (ie attached on a per-import basis) or if I should apply them globally to any selected import button
func start_query(import_id:String, group_id:String="", tags_all:Array=[], tags_any:Array=[], tags_none:Array=[]) -> void:
	if starting_load_process: return
	if import_id == "": return
	starting_load_process = true
	stop_threads()
	
  # temp variables
	var hash_arr:Array = []
	var images_per_page:int = Globals.settings.images_per_page
	var num_threads:int = Globals.settings.load_threads
	var current_sort:int = Globals.settings.current_sort
	var current_order:int = Globals.settings.current_order
	var current_page:Array = [curr_page_number, import_id]
	var thumbnail_path:String = Globals.settings.thumbnail_path
	var temp_query_settings = [import_id, group_id, tags_all, tags_any, tags_none]
	
  # calculate offset
	database_offset = (curr_page_number-1) * images_per_page

  # query database
	var count_results:bool = not(temp_query_settings == last_query_settings)
	last_query_settings = temp_query_settings
	
	# total_count is only used for updating import button, so attach that information to the import buttons themselves, have them query it when the user clicks one

	hash_arr = Database.QueryDatabase(import_id, database_offset, images_per_page, tags_all, tags_any, tags_none, current_sort, current_order, count_results, group_id)
	queried_image_count = Database.GetLastQueriedCount() # just returns a private int, will be updated by the QueryDatabase() call if count_results is true (ie when query settings have changed)
	queried_page_count = ceil(float(queried_image_count)/float(images_per_page)) as int
	Signals.emit_signal("max_pages_changed", queried_page_count)
	
	#print(count_results)
	#print(queried_image_count)
	#print(queried_page_count)
	
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
	
	self.call_deferred("_threadsafe_clear", import_id, curr_page_number, curr_page_image_count, queried_page_count, num_threads) 
	
# threading
func stop_threads() -> void:
	stopping_load_process = true
	for t in load_threads.size():
		stop_thread(t)

func stop_thread(thread_id:int) -> void:
	if load_threads[thread_id].is_active() or load_threads[thread_id].is_alive():
		load_threads[thread_id].wait_to_finish()

func _threadsafe_clear(import_id:String, page_number:int, image_count:int, page_count:int, num_threads:int) -> void:
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
	prepare_thumbnail_loading(import_id, page_number, num_threads)

func prepare_thumbnail_loading(import_id:String, page_number:int, num_threads:int) -> void:
	load_threads.clear()
	for i in num_threads: 
		load_threads.append(Thread.new())
	
	ii.lock() ; item_index = 0 ; ii.unlock()
	tq.lock()
	thumb_queue.clear()
	thumb_queue = page_history[[page_number, import_id]].duplicate()
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
			var image_hash:String = thumb_queue.pop_front()
			tq.unlock() ; ii.lock()
			var index:int = item_index
			item_index += 1
			ii.unlock()
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
		
		if stopping_load_process: return
		var it:ImageTexture = ImageTexture.new()
		it.create_from_image(i, 0) # FLAGS # 4
		it.set_meta("image_hash", image_hash)
		
		th.lock() ; thumb_history[image_hash] = it ; th.unlock()
		
		if stopping_load_process: return
		_threadsafe_set_icon(image_hash, index)

func _threadsafe_set_icon(image_hash:String, index:int, failed:bool=false) -> void:
	var im_tex:Texture
	if failed: im_tex = icon_broken
	else:
		th.lock()
		im_tex = thumb_history[image_hash]
		th.unlock()
	
	if stopping_load_process: return
	sc.lock()
	self.set_item_icon(index, im_tex)
	var size:String = Database.GetFileSize(image_hash)
	set_item_tooltip(index, "hash: " + image_hash + "\nsize: " + ("-1" if size == "" else String.humanize_size(size.to_int())))
	sc.unlock()

var selected_items:Dictionary = {}
var last_index:int = 0
var called_already:bool = false
func _on_thumbnails_multi_selected(index:int, selected:bool) -> void:
	last_index = index
	if called_already: return
	called_already = true
	call_deferred("select_items")
	
func select_items() -> void:
	selected_items.clear()
	var arr_index:Array = self.get_selected_items()
	if arr_index.size() == 0: return
	for i in arr_index.size():
		selected_items[arr_index[i]] = page_history[[curr_page_number, Globals.current_import_id]][arr_index[i]]
	
	var image_hash:String = page_history[[curr_page_number, Globals.current_import_id]][last_index]
	var paths:Array = Database.GetPaths(image_hash)
	if not paths.empty():
		var f:File = File.new()
		for path in paths:
			if f.file_exists(path):
				Signals.emit_signal("load_full_image", path)
				Signals.emit_signal("load_image_tags", image_hash, selected_items)
				Signals.emit_signal("create_path_buttons", image_hash, paths)
				break
	called_already = false
