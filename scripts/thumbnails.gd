extends ItemList

# constants
const icon_buffering:StreamTexture = preload("res://assets/buffer-01.png")

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
func _curr_page_changed(new_page:int) -> void: curr_page_number = new_page

# initialization
func _ready() -> void:
	Signals.connect("page_label_ready", self, "_page_label_ready")
	Signals.connect("page_changed", self, "_curr_page_changed")
	
	var s:String = "".humanize_size(1247843)
	print(s)

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
	start_query(Globals.current_type_id, Globals.current_load_id, tags_all, tags_any, tags_none)

func start_query(type_id:int, load_id:String, tags_all:Array=[], tags_any:Array=[], tags_none:Array=[]) -> void:
	if starting_load_process: return
	if load_id == "" or type_id < 0: return
	starting_load_process = true
	stop_threads()
	
  # temp variables
	var hash_arr:Array = []
	var images_per_page:int = Globals.settings.images_per_page
	var num_threads:int = Globals.settings.load_threads
	var current_sort:int = Globals.settings.current_sort
	var current_order:int = Globals.settings.current_order
	var current_page:Array = [curr_page_number, load_id, type_id]
	var thumbnail_path:String = Globals.settings.thumbnail_path
	var temp_query_settings = [database_offset, images_per_page, tags_all, tags_any, tags_none, current_sort, current_order]
	
  # calculate offset
	database_offset = (curr_page_number-1) * images_per_page

  # query database
	var count_results:bool = temp_query_settings == last_query_settings
	last_query_settings = temp_query_settings
	
	if type_id == Globals.TypeId.All: pass	
		# total_count = # even col.Count(Query.All()) is slow, so instead need to manually keep track of the count and store it in a meta-info collection
		# hash_arr = # query the full database with current offset and settings and get 1 page of hashes
		# queried_count = # get the full count of all images found by the previous query (optimization::should only count images if something has changed from the previous query) (could make a dict{} of query settings and call .hash() on it, then if hash is different from previous one, update it and get the count again)
	elif type_id == Globals.TypeId.ImportGroup: pass 
		# total_count = 
		# hash_arr = 
		# queried_count = 
	elif type_id == Globals.TypeId.ImageGroup: pass 
		# total_count = 
		# hash_arr = 
		# queried_count = 
	queried_page_count = ceil(float(queried_image_count)/float(images_per_page)) as int
	
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
	
	# self.call_deferred("_threadsafe_clear", args, ...)
	
# threading
func stop_threads() -> void: pass



