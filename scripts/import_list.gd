extends Control

onready var button_list:HBoxContainer = $margin/hbox/scroll/button_list
onready var all_button:Button = $margin/hbox/all_button

onready var manager_thread:Thread = Thread.new()
onready var argument_mutex:Mutex = Mutex.new()

enum status { INACTIVE = 0, ACTIVE, PAUSED, CANCELED }
enum results { CONTINUE = 0, EMPTY }

var buttons:Dictionary = {}

var argument_queue:Array = []		# fifo queue of arguments to be processed by threads
var thread_pool:Array = []			# the threads to use for processing
var thread_status:Array = []		# the status of each thread (see status dict)
var thread_args:Array = []			# the arguments associated with a specific thread  (import_id for importing)

var last_arg = null					# import_id for this script

var delay_time:int = 50				# how long to wait in the thread loop before checking argument_queue again
var max_total_threads:int = 4		# the max number of threads that can run simultaneously
var max_threads_per_import:int = 3	#
var active_threads:int = 0			# the current number of active (hose currently processing) threads

var stop_manager:bool = false
var pause_manager:bool = false
var manager_done:bool = false

var last_selected_tab:String = ""

func _ready() -> void:
	Signals.connect("new_import_started", self, "create_new_tab_button")
	Signals.connect("import_info_load_finished", self, "create_tab_buttons")
	Signals.connect("update_import_button", self, "update_button_text")
	buttons["All"] = all_button
	
	create_threads(max_total_threads)

func _on_all_button_button_up() -> void: 
	Signals.emit_signal("tab_button_pressed", "All")
	indicate_selected_button("All")
	
func _on_tab_button_pressed(tab_id:String) -> void: 
	Signals.emit_signal("tab_button_pressed", tab_id)
	indicate_selected_button(tab_id)

func indicate_selected_button(tab_id:String) -> void:
	if last_selected_tab != "": 
		buttons[last_selected_tab].remove_stylebox_override("normal")
		buttons[last_selected_tab].remove_stylebox_override("focus")
		buttons[last_selected_tab].remove_color_override("font_color")
		buttons[last_selected_tab].remove_color_override("font_color_focus")
	
	var color:Color = Color.white
	var sbf:StyleBoxFlat = Globals.make_stylebox(color, 1.0, 0.05, 3)
	buttons[tab_id].add_stylebox_override("normal", sbf)
	buttons[tab_id].add_stylebox_override("focus", sbf)
	buttons[tab_id].add_color_override("font_color", Color.black)
	buttons[tab_id].add_color_override("font_color_focus", Color.black)
	last_selected_tab = tab_id

func create_tab_buttons() -> void: 
	#var import_ids:Array = Database.GetImportIds()
	var tab_ids:Array = Database.GetTabIds()
	update_button_text("All", true, Database.GetSuccessCount("All"), Database.GetTotalCount("All"), "All")
	#for import_id in import_ids:
	# need to either replace some of these counts with queried counts, or only show counts on imports
	# need a way to access meta information for the imports
	for tab_id in tab_ids:
		var tab_type:int = Database.GetTabType(tab_id)
		if tab_type == Globals.Tab.IMPORT_GROUP:
			var import_id:String = Database.GetImportId(tab_id)
			var success_count:int = Database.GetSuccessOrDuplicateCount(import_id)
			var total_count:int = Database.GetTotalCount(import_id)
			var finished:bool = Database.GetFinished(import_id)
			var import_name:String = Database.GetName(import_id)
			create_tab_button(tab_id, finished, total_count, success_count, import_name)
			if not finished:
				append_arg([import_id, total_count])
		elif tab_type == Globals.Tab.IMAGE_GROUP: pass
		elif tab_type == Globals.Tab.TAG: pass
		elif tab_type == Globals.Tab.SIMILARITY: pass
		#var import_id:String = Database.GetImportId(tab_id)
		#var group_id:String = Database.GetGroupId(tab_id)
		#var tag:String = Database.GetTag(tab_id)
		#var similarity_hash:String = Database.GetSimilarityHash(tab_id)

	start_manager()
		
func create_tab_button(tab_id:String, finished:bool, total_count:int, success_count:int, import_name:String) -> void:
	if tab_id == "All": return
	if total_count <= 0: return # remove it from import database (check on c# side when loading though)
	var b:Button = Button.new()
	b.text = "  " + import_name + " (" + String(success_count) + (")  " if finished else (", " + String(success_count) + "/" + String(total_count) + ")  "))
	b.connect("button_up", self, "_on_tab_button_pressed", [tab_id])
	button_list.add_child(b)
	buttons[tab_id] = b

func create_new_tab_button(import_id:String, count:int, import_name:String) -> void:
	if count <= 0: return
	if import_id == "": return
	if argument_queue.has(import_id): return
	
	var tab_id:String = ImageImporter.CreateTabID()
	# this function also needs to take an import name (passed from scanner)
	# tab_id should be used to match settings with the button, and to store button in database
	# import_id is instead used as part of its settings
	
	var b:Button = Button.new()
	b.text = "  " + import_name + " (0/" + String(count) + ")  "
	b.connect("button_up", self, "_on_tab_button_pressed", [tab_id])
	button_list.add_child(b)
	buttons[tab_id] = b
	Database.CreateTab(tab_id, Globals.Tab.IMPORT_GROUP, count, import_id, import_name, "", "", "", null, null, null)
	ImageScanner.CommitImport()
	append_arg(tab_id)
	start_manager()

func update_button_text(tab_id:String, finished:bool, success_count:int, total_count:int, import_name:String) -> void:
	if not finished: buttons[tab_id].text = "  %s (%d/%d)  " % [import_name, success_count, total_count]
	else: buttons[tab_id].text = "  %s (%d)  " % [import_name, success_count]


func create_threads(num_threads:int) -> void:
	if num_threads == thread_pool.size(): return
	max_total_threads = num_threads
	
	if num_threads > thread_pool.size():
		for i in num_threads - thread_pool.size():
			thread_pool.push_back(Thread.new())
			thread_status.push_back(status.INACTIVE)
		thread_args.resize(num_threads)
	else:
		for i in range(num_threads, thread_pool.size()):
			if thread_status[i] == status.ACTIVE:
				thread_status[i] = status.CANCELED
		for i in range(num_threads, thread_pool.size()):
			if thread_pool[i].is_active() or thread_pool[i].is_alive():
				thread_pool[i].wait_to_finish()
		thread_pool.resize(num_threads)
		thread_status.resize(num_threads)
		
		var removed_args:Array = thread_args.slice(num_threads, thread_args.size()-1, 1, true)			
		thread_args.resize(num_threads)
		for arg in removed_args:
			if not thread_args.has(arg):
				argument_queue.push_front(arg)

func start_manager() -> void:
	if not manager_thread.is_active(): 
		manager_thread.start(self, "_manager_thread")

func start(num_threads:int) -> void:
	for i in num_threads:
		start_one()

func start_one() -> void:
	if active_threads == max_total_threads: return
	for thread_id in thread_pool.size():
		if thread_status[thread_id] == status.INACTIVE:
			thread_status[thread_id] = status.ACTIVE
			thread_pool[thread_id].start(self, "_thread", thread_id)
			active_threads += 1
			break

func pause_all() -> void: 
	for thread_id in thread_status.size():
		pause(thread_id)

func pause(thread_id:int) -> void:
	if thread_id >= max_total_threads: return
	if thread_status[thread_id] > status.PAUSED: return
	if thread_status[thread_id] < status.ACTIVE: return
	
	if thread_status[thread_id] == status.PAUSED:
		thread_status[thread_id] = status.ACTIVE
	else: thread_status[thread_id] = status.PAUSED

func cancel_all() -> void:
	for thread_id in thread_status.size():
		thread_status[thread_id] = status.CANCELED
	for thread_id in thread_pool.size():
		_stop(thread_id)

func cancel(thread_id:int) -> void:
	if thread_id >= max_total_threads: return
	if thread_status[thread_id] == status.INACTIVE: return
	thread_status[thread_id] = status.CANCELED
	_stop(thread_id)

func _stop(thread_id:int) -> void:
	if thread_pool[thread_id].is_active() or thread_pool[thread_id].is_alive():
		thread_pool[thread_id].wait_to_finish()
		thread_status[thread_id] = status.INACTIVE
		active_threads -= 1

func append_arg(arg) -> void:
	argument_mutex.lock()
	argument_queue.push_back(arg)
	argument_mutex.unlock()

func append_args(args:Array) -> void:
	argument_mutex.lock()
	argument_queue.append_array(args)
	argument_mutex.unlock()

func get_args():
	var args = null
	argument_mutex.lock()
	if not argument_queue.empty():
		args = argument_queue.pop_front()
	argument_mutex.unlock()
	return args

func arguments_empty() -> bool:
	argument_mutex.lock()
	var result:bool = argument_queue.empty()
	argument_mutex.unlock()
	return result

func set_thread_args(thread_id:int, args) -> void:
	argument_mutex.lock()
	if thread_id >= thread_args.size(): 
		argument_mutex.unlock()
		return
	thread_args[thread_id] = args
	argument_mutex.unlock()
	
func get_thread_args(thread_id:int):
	argument_mutex.lock()
	if thread_id >= thread_args.size(): 
		argument_mutex.unlock()
		return null
	var result = thread_args[thread_id]
	argument_mutex.unlock()
	return result

# not consistent at assigning threads, need to rethink logic at some point
func _manager_thread() -> void:
	var current_count:int = 0
	var current_tab_id = get_args()
	
	while not stop_manager:
		if not pause_manager:
			if current_tab_id == null: break
			for thread_id in thread_args.size():
				#print(thread_args)
				if current_tab_id == null: break
				if current_count == max_threads_per_import: break
				if get_thread_args(thread_id) == null:
					set_thread_args(thread_id, current_tab_id)
					current_count += 1
					start_one()
				
		OS.delay_msec(delay_time)
		if current_count == max_threads_per_import:
			current_count = 0
			current_tab_id = get_args()
			if current_tab_id == null: break
			
	call_deferred("_manager_done")

func _manager_done() -> void:
	if manager_thread.is_active() or manager_thread.is_alive():
		manager_thread.wait_to_finish()

# not consistent at calling FinishImport (I think)
func _thread(thread_id:int) -> void:
	while thread_status[thread_id] != status.CANCELED:
		if thread_status[thread_id] != status.PAUSED:
			var args = get_thread_args(thread_id)
			if args != null:
				var tab_id:String = args
				var result:int = ImageImporter.ImportImage(tab_id)
				if result == results.EMPTY:
					argument_mutex.lock()
					thread_args[thread_id] = null
					if not thread_args.has(args):
						argument_mutex.unlock()
						ImageImporter.FinishImport(tab_id)
					else: argument_mutex.unlock()
					break
		OS.delay_msec(delay_time)
		if thread_id >= max_total_threads: break
	call_deferred("_done", thread_id)

func _done(thread_id:int) -> void:
	_stop(thread_id)

