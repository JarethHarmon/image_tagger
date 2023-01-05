extends ItemList

# one issue with the current approach to page history is that if the page has changed by a method not included
# in the hash (e.g. additional/removed tags on images when sorting by tag count) ; the page will not update, and 
# cannot be updated for the current query settings

# one option is to just allow manual refreshes with F5 and the search button; though it requires a manual action
# for each page that was previously loaded into memory for the tab with changed images

# another option is to always re-query pages sorted by tag count, or any similar concepts in the future (like ratings)

# another option is to only re-query when it needs to, but that is not at all easy to determine 
# 	(unless I am just too tired and missing something obvious)


const icon_broken:StreamTexture = preload("res://assets/icon-broken.png") 
const icon_buffering:StreamTexture = preload("res://assets/buffer-01.png")

enum Status { INACTIVE, ACTIVE, CANCELED, ERROR }

onready var lt:Mutex = Mutex.new()			# mutex for interacting with loaded_thumbnails
onready var sc:Mutex = Mutex.new()			# mutex for interacting with scene
onready var tq:Mutex = Mutex.new()			# mutex for interacting with thumb_queue
onready var th:Mutex = Mutex.new()			# mutex for interacting with thumb_history

onready var buffer:CenterContainer = $cc

var tab_history:Dictionary = {} 			# { tab_id:{ "scroll":scroll_percentage , "page":page } }
var thumb_history:Dictionary = {}			# { image_hash:ImageTexture }
var current_query:Dictionary = {}			# the query currently being processed
var current_hashes:Dictionary = {}			# { index:image_hash } the image_hashes and their index for the currently viewed page
var selected_items:Dictionary = {}

var loaded_thumbnails:Array = []			# [ fifo_queue_version_of_thumb_history.keys() ] 
var thumb_queue:Array = []					# [ hashes_waiting_to_load ]
var thumbnail_threads:Array = []			# [ threads_used_for_loading_thumbnails ]  
var last_query_settings:Array = []			# the settings used for the last query (used to avoid counting query results multiple times)

var stop_threads:bool = false				# whether the thumbnail threads should stop processing
var called_already:bool = false
var ctrl_pressed:bool = false
var shift_pressed:bool = false

var selected_item:int = 0
var last_selected_item:int = 0
var last_index:int = 0
var curr_page_number:int = 1				# 
var curr_page_image_count:int = 0
var total_image_count:int = 0
var queried_page_count:int = 1
var queried_image_count:int = 1
var database_offset:int = 0

var page_label:Label
func _page_label_ready(node_path:NodePath) -> void: page_label = get_node(node_path) 
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
	Signals.connect("search_pressed", self, "prepare_query")
	Signals.connect("select_all_pressed", self, "select_all_items")
	Signals.connect("deselect_all_pressed", self, "deselect_all")
	Signals.connect("toggle_thumbnail_tooltips", self, "_toggle_thumbnail_tooltips")
	self.get_v_scroll().connect("scrolling", self, "_scrolling")
	self.get_v_scroll().connect("value_changed", self, "_scrolling")	

#-----------------------------------------------------------------------#
#					Querying and Loading Thumbnails						#
#-----------------------------------------------------------------------#
var first_time:bool = true # prevents scroll from being reset when going back to the same page (would not be needed if I fixed logic to not call this function twice)
func prepare_query(tags_all:Array=[], tags_any:Array=[], tags_none:Array = [], tags_complex:Array = [], new_query:bool=true) -> void: 
	var query:Dictionary = {
		"tab_id" : Globals.current_tab_id,
		"tags_all" : tags_all,
		"tags_any" : tags_any,
		"tags_none" : tags_none,
		"tags_complex" : tags_complex,
	}
	if new_query:
		curr_page_number = 1
		queried_page_count = 0
		total_image_count = 0

		if tab_history.has(query.tab_id):
			curr_page_number = tab_history[query.tab_id].page
		
		# EXTREMELY IMPORTANT NOTE: SIGNALS, BY DEFAULT, EXECUTE IMMEDIATELY, IN-PLACE, IF ON THE MAIN THREAD
		# THIS MEANS THAT WITHOUT A DEFERRED CALL THIS FUNCTION ACTUALLY ITERATES INTO THIS CONDITION, RECIEVES THE SIGNAL
		# THEN ITERATES AGAIN TO COMPLETION (TAKING THE ELSE CONDITION) BEFORE RETURNING AND FINISHING THIS SECTION	
		# DOES NOT CAUSE MAJOR ISSUES ONLY BECAUSE THIS CONDITION RETURNS
		Signals.call_deferred("emit_signal", "page_changed", curr_page_number)
		first_time = false
		return # not ideal solution; need to restructure overall order; for now prevents querying twice
		# was: button -> sp -> pq(true)(qt) -> cpc -> pc -> sp -> pq(false) -> qt
		# now: button -> sp -> pq(true) -> cpc -> pc -> sp -> pq(false) -> qt
	else:
		if first_time:
			tab_history[query.tab_id].scroll = 0.0	
	current_query = query
	yield(get_tree(), "idle_frame")
	yield(get_tree(), "idle_frame") 
	var thread:Thread = Thread.new()
	
	# disable sort buttons for similarity tabs
	if Globals.current_tab_type == Globals.Tab.SIMILARITY: Signals.emit_signal("switch_sort_buttons", true)
	else: Signals.emit_signal("switch_sort_buttons", false)
	
	thread.call_deferred("start", self, "_query_thread", [thread, query])
	#thread.start(self, "_query_thread", [thread, query])
	first_time = true

func _is_invalid_query(thread:Thread, query:Dictionary) -> bool:
	if query == current_query: return false
	thread.call_deferred("wait_to_finish")
	return true

func _query_thread(args:Array) -> void:
	buffer.rect_min_size = self.rect_size / 4
	buffer.show()
	var thread:Thread = args[0]
	var query:Dictionary = args[1]
	var tab_id:String = query.tab_id
	var tags_all:Array = query.tags_all
	var tags_any:Array = query.tags_any
	var tags_none:Array = query.tags_none
	var tags_complex:Array = query.tags_complex
	
	if _is_invalid_query(thread, query): return
	
  # set temp variables	
	var image_hashes:Array = []
	var tab_type:int = Globals.current_tab_type
	var images_per_page:int = Globals.settings.images_per_page
	var current_sort:int = Globals.settings.current_sort
   # order by descending if Similarity Tab
	var current_order:int = Globals.settings.current_order if tab_type < Globals.Tab.SIMILARITY else Globals.OrderBy.Descending
	var temp_query_settings = [tab_id, tags_all, tags_any, tags_none, tags_complex]
	var num_threads:int = Globals.settings.load_threads
	var similarity:int = Globals.current_similarity
	
  # calculate the offset and whether it should count the query
	database_offset = (curr_page_number-1) * images_per_page
	var count_results:bool = not(temp_query_settings == last_query_settings)
	last_query_settings = temp_query_settings
	
  # query the database
#	if tags_all.empty() and tags_any.empty() and tags_none.empty():
#		var import_id:String = ""
#		var temp =  Database.GetImportId(tab_id)
#		if temp == null or temp == "": temp = "All"
#		import_id = temp
#
#		queried_image_count = Database.GetSuccessOrDuplicateCount(import_id)
#		var lqc:Array = [tab_id, curr_page_number, current_sort, current_order, tags_all, tags_any, tags_none, queried_image_count] # add filters to this once implemented
#		var lqh:int = lqc.hash()
#
#		# print(tab_id, ": ", Storage.HasPage(lqh))
#		if Storage.HasPage(lqh):
#			image_hashes = Storage.GetPage(lqh)
#			Storage.UpdatePageQueuePosition(lqh)
#			Database.PopulateDictHashes(image_hashes)

	if image_hashes.empty():	
		if _is_invalid_query(thread, query): return
		#image_hashes = Database.QueryDatabase(tab_id, database_offset, images_per_page, tags_all, tags_any, tags_none, tags_complex, current_sort, current_order, count_results, similarity)
		#image_hashes = QueryManager.TempConstructQueryInfo(tab_id, database_offset, images_per_page, tags_all, tags_any, tags_none, tags_complex, current_sort, current_order, count_results, similarity)
		#var qid:String = image_hashes.pop_back()
		#queried_image_count = QueryManager.GetLastQueriedCount(qid)
		#queried_image_count = Database.GetLastQueriedCount()
#		var lqc:Array = [tab_id, curr_page_number, current_sort, current_order, tags_all, tags_any, tags_none, queried_image_count] # add filters to this once implemented
#		var lqh:int = lqc.hash()
		# > ImageColor accounts for all Rating sorts (should eventually group all of these together)
		# similarity tabs do not support sort currently, so they do not need this check
#		if (Globals.current_tab_type == Globals.Tab.SIMILARITY) or (current_sort != Globals.SortBy.TagCount and not current_sort > Globals.SortBy.ImageColor):# and current_sort != Globals.SortBy.Random: # need to add manual refresh button instead
#			Storage.AddPage(lqh, image_hashes)
		if _is_invalid_query(thread, query): return
	
  # get the correct values for page variables
	curr_page_image_count = image_hashes.size()
	queried_page_count = ceil(float(queried_image_count)/float(images_per_page)) as int 
	if _is_invalid_query(thread, query): return
	Signals.emit_signal("max_pages_changed", queried_page_count)
	Signals.emit_signal("image_count_changed", queried_image_count)
	
	create_thumbnail_threads(num_threads)
	if _is_invalid_query(thread, query): return
	
	call_deferred("_threadsafe_clear", query, image_hashes, curr_page_image_count)
	thread.call_deferred("wait_to_finish")
	buffer.hide()

func _threadsafe_clear(query:Dictionary, image_hashes:Array, image_count:int) -> void:
	sc.lock()
	var scroll_mult:float = 0.0
	if tab_history.has(query.tab_id):
		scroll_mult = tab_history[query.tab_id].scroll
	if self.get_item_count() > 0:
		yield(get_tree(), "idle_frame")
		self.clear()
		yield(get_tree(), "idle_frame")
		yield(get_tree(), "idle_frame")
	for i in image_count:
		self.add_item("", icon_buffering) 
	thumb_queue.clear()
	var vscroll:VScrollBar = self.get_v_scroll()
	yield(get_tree(), "idle_frame")
	vscroll.set_value(vscroll.max_value * scroll_mult)
	sc.unlock()
	if query != current_query: return
	start_loading(query, image_hashes, image_count)

func start_loading(query:Dictionary, image_hashes:Array, image_count:int) -> void:
	#var max_loaded_thumbnails:int = Globals.settings.max_loaded_thumbnails
	#var thumbnail_path:String = Globals.settings.thumbnail_path
	var max_loaded_thumbnails:int = Global.Settings.MaxThumbnailsToStore
	var thumbnail_path:String = Global.GetThumbnailPath()

  # set current_hashes, iterate it to look for any thumbnails that are already loaded, set them and update their position in queue if found
	var dict_image_hashes:Dictionary = {}
	for idx in image_hashes.size():
		dict_image_hashes[idx] = image_hashes[idx]
	current_hashes = dict_image_hashes.duplicate()
	if _set_thumbnails_from_history(query, dict_image_hashes): 
		sc.unlock() ; return
	
  # remove hashes/thumbnails from history and queue if necessary
	lt.lock()
	var tq_size:int = loaded_thumbnails.size()
	var di_size:int = dict_image_hashes.size()
	if tq_size + di_size > max_loaded_thumbnails:
		var kept_hashes:Array = loaded_thumbnails.slice(di_size, tq_size)
		th.lock()
		for i in di_size:
			thumb_history.erase(loaded_thumbnails[i])
		th.unlock()
		loaded_thumbnails = kept_hashes
	lt.unlock()
	tq.lock()
	for idx in dict_image_hashes:
		thumb_queue.push_back([idx, dict_image_hashes[idx]])
	tq.unlock()
	if query != current_query: 
		sc.unlock() ; return
	
  # start the thumbnail threads to load the remaining thumbnails
	_start_thumbnail_loading_threads()
	sc.unlock()

func _set_thumbnails_from_history(query:Dictionary, image_hashes:Dictionary) -> bool:
	var temp:Array = image_hashes.keys()
	for idx in temp:
		if query != current_query: return true
		var image_hash:String = image_hashes[idx]
		image_hashes.erase(image_hash)
		th.lock()
		if not thumb_history.has(image_hash): 
			th.unlock()
			continue
		var im_tex:Texture = thumb_history[image_hash].texture
		th.unlock() ; lt.lock()
		loaded_thumbnails.erase(image_hash)
		loaded_thumbnails.push_back(image_hash)
		lt.unlock()
		var text:String = ""
		if Globals.current_tab_type == Globals.Tab.SIMILARITY:
			var compare_hash:String = MetadataManager.GetTabSimilarityHash(Globals.current_tab_id)
			var similarity:float = DatabaseManager.GetAveragedSimilarityTo(compare_hash, image_hash)
			#var compare_hash:String = Database.GetSimilarityHash(Globals.current_tab_id)
			#var similarity:float = Database.GetAveragedSimilarityTo(compare_hash, image_hash)
			text = "%1.2f" % [similarity] + "%"
		if query != current_query: return true
		self.set_item_icon(idx, im_tex)
		self.set_item_text(idx, text)
	return false
	
func _start_thumbnail_loading_threads() -> void:
	for thread_id in thumbnail_threads.size():
		thumbnail_threads[thread_id].start(self, "_thread", thread_id)

func create_thumbnail_threads(num_threads:int) -> void:
	stop_thumbnail_threads()
	thumbnail_threads.clear()
	for thread_id in num_threads:
		thumbnail_threads.push_back(Thread.new())

func stop_thumbnail_threads() -> void:
	stop_threads = true
	for thread_id in thumbnail_threads.size():
		stop_thumbnail_thread(thread_id)
	stop_threads = false

func stop_thumbnail_thread(thread_id:int) -> void:
	if thumbnail_threads[thread_id].is_active() or thumbnail_threads[thread_id].is_alive():
		thumbnail_threads[thread_id].wait_to_finish()

func get_args():
	tq.lock()
	var result = null
	if not thumb_queue.empty():
		result = thumb_queue.pop_front()
	tq.unlock()
	return result

func append_args(args):
	tq.lock()
	thumb_queue.append(args)
	tq.unlock()

func _thread(thread_id:int) -> void:
	while not stop_threads:
		var args = get_args()
		if args == null: break
		var index:int = args[0]
		var image_hash:String = args[1]
		load_thumbnail(image_hash, index)
	call_deferred("stop_thumbnail_thread", thread_id)

func load_thumbnail(image_hash:String, index:int) -> void:
	th.lock()
	if thumb_history.has(image_hash):
		if thumb_history[image_hash].texture != null:
			th.unlock()
			if stop_threads: return
			_threadsafe_set_icon(image_hash, index)
			return
	th.unlock()
	_set_metadata(image_hash)
	
	var f:File = File.new()
	#var p:String = Globals.settings.thumbnail_path.plus_file(image_hash.substr(0, 2)).plus_file(image_hash) + ".thumb"
	var p:String = Global.Settings.GetThumbnailPath().plus_file(image_hash.substr(0, 2)).plus_file(image_hash) + ".thumb"
	var e:int = f.open(p, File.READ)
	
	if stop_threads: return
	if e != OK: 
		_threadsafe_set_icon(image_hash, index, true)
		return

	if stop_threads: return

	var i:Image = Image.new()
	var b:PoolByteArray = f.get_buffer(f.get_len())
	e = i.load_webp_from_buffer(b)

	if stop_threads: return
	var it:ImageTexture = ImageTexture.new()
	it.create_from_image(i, 0) # FLAGS # 4
	it.set_meta("image_hash", image_hash)
	
	th.lock() ; thumb_history[image_hash]["texture"] = it ; th.unlock()
	lt.lock() ; loaded_thumbnails.push_back(image_hash) ; lt.unlock()
	
	if stop_threads: return
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
	if stop_threads: return
	sc.lock()
	self.set_item_icon(index, im_tex)
	#if Globals.settings.show_thumbnail_tooltips:
	#	var tooltip:String = _create_tooltip(image_hash, dict, index)
	#	set_item_tooltip(index, tooltip)

	# currently it calculates the similarity 1-by-1 while setting the thumbnail, which
	#	means individual database calls to retrieve the hash info, and then similarity comparisons
	# I could instead either:
	#	A. query the hash info all at once, then re-iterate the thumbnails and update their labels
	#	B. avoid querying the hash info at all, remove the concept of similarity labels, and instead
	#		display the similarity percentage in the preview window on a per-image basis 
	if Globals.current_tab_type == Globals.Tab.SIMILARITY:
		#var compare_hash:String = Database.GetSimilarityHash(Globals.current_tab_id)
		var compare_hash:String = MetadataManager.GetTabSimilarityHash(Globals.current_tab_id)
		var similarity:float = 0.0
		if Globals.current_similarity == Globals.Similarity.AVERAGED:
			#similarity = Database.GetAveragedSimilarityTo(compare_hash, image_hash)
			similarity = DatabaseManager.GetAveragedSimilarityTo(compare_hash, image_hash)
		elif Globals.current_similarity == Globals.Similarity.AVERAGE:
			#similarity = Database.GetAverageSimilarityTo(compare_hash, image_hash)
			similarity = DatabaseManager.GetAverageSimilarityTo(compare_hash, image_hash)
		elif Globals.current_similarity == Globals.Similarity.WAVELET:
			#similarity = Database.GetWaveletSimilarityTo(compare_hash, image_hash)
			similarity = DatabaseManager.GetWaveletSimilarityTo(compare_hash, image_hash)
		else:
			#similarity = Database.GetDifferenceSimilarityTo(compare_hash, image_hash)
			similarity = DatabaseManager.GetDifferenceSimilarityTo(compare_hash, image_hash)
		set_item_text(index, "%1.2f" % [similarity] + "%")
	sc.unlock()


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

func _set_metadata(image_hash:String) -> void:
	#var size:String = Database.GetFileSize(image_hash)
	#var image_name:String = Database.GetImageName(image_hash)
	#var diff_hash:String = Database.GetDiffHash(image_hash)
	#var color_hash:Array = Database.GetColorHash(image_hash)
	#var creation_time:String = Database.GetCreationTime(image_hash)
	#var paths:Array = Database.GetHashPaths(image_hash)# (create paths section again)
	th.lock()
	# get dimensions too
	var dict:Dictionary = {
		"texture" : null,
	#	"size" : size,
	#	"image_name" : image_name,
	#	"diff_hash" : diff_hash,
	#	"color_hash" : color_hash,
	#	"creation_time" : creation_time,
	#	"paths" : paths
	}
	thumb_history[image_hash] = dict
	th.unlock()
	
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
		selected_items[arr_index[i]] = current_hashes[arr_index[i]]
	#color_all()
	
	var image_hash:String = current_hashes[last_index]
	MetadataManager.LoadCurrentImageInfo(image_hash)
	#Database.LoadCurrentHashInfo(image_hash)
	#print(image_hash)
	var paths:Array = MetadataManager.GetCurrentPaths()#Database.GetHashPaths(image_hash)
	var imports:Array = MetadataManager.GetCurrentImports()#Database.GetImportIdsFromHash(image_hash)
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
	for i in curr_page_image_count: 
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

onready var thumb_size:HSlider = self.get_parent().get_node("sort_buttons/thumbnail_size")
onready var thumb_size_entry:SpinBox = self.get_parent().get_node("sort_buttons/thumbnail_size_entry")

func _on_thumbnail_size_value_changed(value:int) -> void:
	self.fixed_icon_size = Vector2(value, value)
	self.fixed_column_width = value
	thumb_size_entry.value = value
	Globals.settings.thumbnail_width = value
	
func _on_thumbnail_size_entry_value_changed(value:int) -> void:
	self.fixed_icon_size = Vector2(value, value)
	self.fixed_column_width = value
	thumb_size.value = value
	Globals.settings.thumbnail_width = value

func _toggle_thumbnail_tooltips() -> void:
	var show_tooltips:bool = Globals.settings.show_thumbnail_tooltips
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
