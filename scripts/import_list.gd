extends Control

enum Status { INACTIVE = 0, ACTIVE, PAUSED, CANCELED, ERROR=-1 }
enum Results { CONTINUE = 0, EMPTY }

onready var button_list:HBoxContainer = $margin/hbox/scroll/button_list
onready var all_button:Button = $margin/hbox/all_button

onready var import_section_thread:Thread = Thread.new()
onready var section_mutex:Mutex = Mutex.new()
onready var manager_thread:Thread = Thread.new()
onready var argument_mutex:Mutex = Mutex.new()
onready var count_mutex:Mutex = Mutex.new()
onready var status_mutex:Mutex = Mutex.new()

var similarity_stylebox:StyleBoxFlat
var similarity_stylebox_selected:StyleBoxFlat
var similarity_stylebox_hover:StyleBoxFlat
var importing_stylebox:StyleBoxFlat
var importing_stylebox_selected:StyleBoxFlat
var importing_stylebox_hover:StyleBoxFlat

var thread_count:Dictionary = {} 	# { import_id:num_thread_in_use }
var buttons:Dictionary = {}

var argument_queue:Array = []		# fifo queue of arguments to be processed by threads
var thread_pool:Array = []			# the threads to use for processing
var thread_status:Array = []		# the status of each thread (see status dict)
var thread_args:Array = []			# the arguments associated with a specific thread  (import_id for importing)
var finished_sections:Array = []

var delay_time:int = 50				# how long to wait in the thread loop before checking argument_queue again
var active_threads:int = 0			# the current number of active (hose currently processing) threads

var stop_import_section:bool = false
var stop_manager:bool = false
var pause_manager:bool = false
var manager_done:bool = false

var last_selected_tab:String = ""

func _ready() -> void:
	Signals.connect("new_import_started", self, "create_new_tab_button")
	Signals.connect("import_info_load_finished", self, "create_tab_buttons")
	Signals.connect("increment_all_button", self, "increment_all_button")
	Signals.connect("increment_import_buttons", self, "increment_import_buttons")
	Signals.connect("finish_import_buttons", self, "finish_import_buttons")
	Signals.connect("create_similarity_tab", self, "create_new_similarity_tab")
	Signals.connect("toggle_tab_section", self, "_toggle_tab_section")
	Signals.connect("settings_loaded", self, "_settings_loaded")
	buttons[Global.ALL] = all_button

func _settings_loaded() -> void:
	similarity_stylebox = Globals.make_stylebox(Color(0.375, 0.125, 0.375), 0.85, 0.2, 3)
	similarity_stylebox_selected = Globals.make_stylebox(Color(0.375, 0.125, 0.375), 2.9, 0.2, 3)
	similarity_stylebox_hover = Globals.make_stylebox(Color(0.375, 0.125, 0.375), 1.6, 0.2, 3)
	importing_stylebox = Globals.make_stylebox(Color("#e0d040"), 0.85, 0.2, 3)
	importing_stylebox_selected = Globals.make_stylebox(Color("#e0d040"), 2.0, 0.2, 3)
	importing_stylebox_hover = Globals.make_stylebox(Color("#e0d040"), 1.5, 0.2, 3)
	create_threads(Global.GetMaxImportThreads())
	
func _toggle_tab_section(_visible:bool) -> void: 
	self.visible = _visible

func _tab_button_press(tab_id:String) -> void:
	Globals.current_tab_id = tab_id
	Global.SetCurrentTabId(tab_id)
	Globals.current_tab_type = MetadataManager.GetTabType(tab_id)
	ThumbnailManager.UpdateImportId(tab_id)
	if Globals.current_tab_type == Globals.TabType.SIMILARITY: Signals.emit_signal("switch_sort_buttons", true)
	else: Signals.emit_signal("switch_sort_buttons", false)
	Signals.emit_signal("check_tab_history")

func _on_all_button_button_up() -> void: 
	_tab_button_press(Global.ALL)
	indicate_selected_button(Global.ALL)
	
func _on_tab_button_pressed(tab_id:String) -> void: 
	_tab_button_press(tab_id)
	indicate_selected_button(tab_id)

func indicate_selected_button(tab_id:String) -> void:
	if last_selected_tab != "": 
		var import_id:String = MetadataManager.GetTabImportId(last_selected_tab)
		var finished:bool = true if import_id == null else MetadataManager.GetFinished(import_id)
		
		if buttons.has(last_selected_tab):
			if MetadataManager.GetTabType(last_selected_tab) == Globals.TabType.SIMILARITY:
				buttons[last_selected_tab].add_stylebox_override("normal", similarity_stylebox)
				buttons[last_selected_tab].add_stylebox_override("focus", similarity_stylebox)
				buttons[last_selected_tab].remove_color_override("font_color")
				buttons[last_selected_tab].remove_color_override("font_color_focus")
			elif not finished:
				buttons[last_selected_tab].add_stylebox_override("normal", importing_stylebox)
				buttons[last_selected_tab].add_stylebox_override("focus", importing_stylebox)
				buttons[last_selected_tab].add_color_override("font_color", Color.black)
				buttons[last_selected_tab].add_color_override("font_color_focus", Color.black)
			else:
				buttons[last_selected_tab].remove_stylebox_override("normal")
				buttons[last_selected_tab].remove_stylebox_override("focus")
				buttons[last_selected_tab].remove_color_override("font_color")
				buttons[last_selected_tab].remove_color_override("font_color_focus")
	
	if MetadataManager.GetTabType(tab_id) == Globals.TabType.SIMILARITY:
		buttons[tab_id].add_stylebox_override("normal", similarity_stylebox_selected)
		var color:Color = Color.white
		var sbf:StyleBoxFlat = Globals.make_stylebox(color, 1.0, 0.05, 3)
		buttons[tab_id].add_stylebox_override("focus", sbf)
	else:
		var color:Color = Color.white
		var sbf:StyleBoxFlat = Globals.make_stylebox(color, 1.0, 0.05, 3)
		buttons[tab_id].add_stylebox_override("normal", sbf)
		buttons[tab_id].add_stylebox_override("focus", sbf)
		
	buttons[tab_id].add_color_override("font_color", Color.black)
	buttons[tab_id].add_color_override("font_color_focus", Color.black)
	last_selected_tab = tab_id

func create_tab_buttons() -> void: 
	var tab_ids:Array = MetadataManager.GetTabIds()
	var all_count:int = MetadataManager.GetSuccessCount(Global.ALL)
	update_button_text(Global.ALL, true, all_count, MetadataManager.GetTotalCount(Global.ALL), Global.ALL)
	thread_count[Global.ALL] = all_count

	for tab_id in tab_ids:
		var tab_type:int = MetadataManager.GetTabType(tab_id)
		if tab_type == Globals.TabType.DEFAULT:
			var import_id:String = MetadataManager.GetTabImportId(tab_id)
			var success_count:int = MetadataManager.GetSuccessCount(import_id)
			var total_count:int = MetadataManager.GetTotalCount(import_id)
			var finished:bool = MetadataManager.GetFinished(import_id)
			var processed_count:int = MetadataManager.GetProcessedCount(import_id)
			var tab_name:String = MetadataManager.GetTabName(tab_id)
			var progress_ids:Array = MetadataManager.GetSections(import_id)

			if not finished:
				if progress_ids.empty():
					ImportManager.CompleteImport(import_id)
					finished = true
					
			create_tab_button(tab_id, finished, total_count, success_count, processed_count, tab_name)
			if not finished:
				for progress_id in progress_ids:
					append_arg([import_id, progress_id])

		elif tab_type == Globals.TabType.SIMILARITY: 
			var image_hash:String = MetadataManager.GetTabSimilarityHash(tab_id)
			create_similarity_tab(tab_id, image_hash)

	start_manager()
		
func create_tab_button(tab_id:String, finished:bool, total_count:int, success_count:int, processed_count:int, tab_name:String) -> void:
	if tab_id == Global.ALL: return
	if total_count <= 0: return # remove it from import database (check on c# side when loading though)
	var b:Button = Button.new()
	if finished: b.text = "  " + tab_name + " (" + String(success_count) + ")  "
	else:
		thread_count[tab_id] = processed_count 
		b.text = "  %s (%d/%d)  " % [tab_name, processed_count, total_count]
		b.add_stylebox_override("normal", importing_stylebox)
		b.add_stylebox_override("focus", importing_stylebox)
		b.add_color_override("font_color", Color.black)
		b.add_color_override("font_color_focus", Color.black)

	b.connect("button_up", self, "_on_tab_button_pressed", [tab_id])
	b.connect("gui_input", self, "_on_tab_button_gui_input", [tab_id])
	button_list.add_child(b)
	buttons[tab_id] = b

func create_new_tab_button(count:int, tab_name:String) -> void:
	if count <= 0: return
	var import_id:String = ScanManager.CommitScan(tab_name)
	var tab_id:String = MetadataManager.CreateTab(tab_name, Globals.TabType.DEFAULT, import_id, "", "", "")
	var b:Button = Button.new()
	b.text = "  " + tab_name + " (0/" + String(count) + ")  "
	b.connect("button_up", self, "_on_tab_button_pressed", [tab_id])
	b.connect("gui_input", self, "_on_tab_button_gui_input", [tab_id])
	b.add_stylebox_override("normal", importing_stylebox)
	b.add_stylebox_override("focus", importing_stylebox)
	b.add_color_override("font_color", Color.black)
	b.add_color_override("font_color_focus", Color.black)
	button_list.add_child(b)
	buttons[tab_id] = b
	
	# rename thread_count to image_count or progress_count
	thread_count[tab_id] = 0

	var section_ids:Array = MetadataManager.GetSections(import_id)

	for section_id in section_ids:
		append_arg([import_id, section_id])
	
	start_manager()

func create_similarity_tab(tab_id:String, image_hash:String) -> void:
	if tab_id == "": return
	if image_hash == "": return
	var b:Button = Button.new()
	b.text = "  " + image_hash.substr(0, 16) + "  "
	b.connect("button_up", self, "_on_tab_button_pressed", [tab_id])
	b.connect("gui_input", self, "_on_tab_button_gui_input", [tab_id])
	
	b.add_stylebox_override("normal", similarity_stylebox)
	b.add_stylebox_override("hover", similarity_stylebox_hover)
	b.add_stylebox_override("focus", similarity_stylebox)

	button_list.add_child(b)
	buttons[tab_id] = b

func create_new_similarity_tab(image_hash:String) -> void:
	if image_hash == "": return
	var tab_id:String = MetadataManager.CreateTab("Similarity", Globals.TabType.SIMILARITY, "", "", "", image_hash)
	
	var b:Button = Button.new()
	b.text = "  " + image_hash.substr(0, 16) + "  "
	b.connect("button_up", self, "_on_tab_button_pressed", [tab_id])
	b.connect("gui_input", self, "_on_tab_button_gui_input", [tab_id])
	
	b.add_stylebox_override("normal", similarity_stylebox)
	b.add_stylebox_override("hover", similarity_stylebox_hover)
	b.add_stylebox_override("focus", similarity_stylebox)
	
	button_list.add_child(b)
	buttons[tab_id] = b

func _on_tab_button_gui_input(event:InputEvent, tab_id:String) -> void:
	if event is InputEventMouseButton:
		if event.button_index == BUTTON_RIGHT:
			delete_tab(tab_id)

func delete_tab(tab_id:String) -> void:
	if not buttons.has(tab_id): return
	var button:Button = buttons[tab_id]
	buttons.erase(tab_id)
	button_list.remove_child(button)
	
	MetadataManager.DeleteTab(tab_id)
	if last_selected_tab == tab_id:
		_on_tab_button_pressed("All")

func update_button_text(tab_id:String, finished:bool, success_count:int, total_count:int, import_name:String) -> void:
	if not finished: buttons[tab_id].text = "  %s (%d/%d)  " % [import_name, success_count, total_count]
	else: buttons[tab_id].text = "  %s (%d)  " % [import_name, success_count]

func increment_all_button() -> void:
	count_mutex.lock()
	thread_count["All"] += 1
	var count:int = thread_count["All"]
	buttons["All"].text = "  All (%d)  " % count
	count_mutex.unlock()

func increment_import_buttons(tab_ids:Array) -> void:
	count_mutex.lock()
	for tab_id in tab_ids:
		if thread_count.has(tab_id):
			thread_count[tab_id] += 1
			var count:int = thread_count[tab_id]
			buttons[tab_id].text = "  %s (%d/%d)  " % [MetadataManager.GetTabName(tab_id), count, MetadataManager.GetTotalCount(MetadataManager.GetTabImportId(tab_id))]
	count_mutex.unlock()

func finish_import_buttons(tab_ids:Array) -> void:
	for tab_id in tab_ids:
		buttons[tab_id].text = "  %s (%d)  " % [MetadataManager.GetTabName(tab_id), MetadataManager.GetSuccessCount(MetadataManager.GetTabImportId(tab_id))]
		if not last_selected_tab == tab_id:
			buttons[tab_id].remove_stylebox_override("normal")
			buttons[tab_id].remove_stylebox_override("focus")
			buttons[tab_id].remove_color_override("font_color")
			buttons[tab_id].remove_color_override("font_color_focus")
	if Globals.current_tab_id in tab_ids:
		Signals.emit_signal("image_import_finished", Globals.current_tab_id)

func create_threads(num_threads:int) -> void:
	import_section_thread.start(self, "_import_section_thread")
	if num_threads == thread_pool.size(): return

	if num_threads > thread_pool.size():
		for i in num_threads - thread_pool.size():
			thread_pool.push_back(Thread.new())
			thread_status.push_back(Status.INACTIVE)
		thread_args.resize(num_threads)
	else:
		for i in range(num_threads, thread_pool.size()):
			if thread_status[i] == Status.ACTIVE:
				thread_status[i] = Status.CANCELED
		for i in range(num_threads, thread_pool.size()):
			if thread_pool[i].is_active() or thread_pool[i].is_alive():
				thread_pool[i].wait_to_finish()
		thread_pool.resize(num_threads)
		thread_status.resize(num_threads)
		
		var removed_args:Array = thread_args.slice(num_threads, thread_args.size()-1, 1, true)			
		thread_args.resize(num_threads)
		for arg in removed_args:
			if not thread_args.has(arg) and not arg == null:
				argument_queue.push_front(arg)

func start_manager() -> void:
	if not manager_thread.is_active(): 
		manager_thread.start(self, "_manager_thread")

func start_one() -> void:
	if active_threads == Global.GetMaxImportThreads(): return
	for thread_id in thread_pool.size():
		if get_thread_status(thread_id) == Status.INACTIVE:
			set_thread_status(thread_id, Status.ACTIVE)
			thread_pool[thread_id].start(self, "_thread", thread_id)
			active_threads += 1
			break

func pause_all() -> void: 
	for thread_id in thread_status.size():
		pause(thread_id)

func pause(thread_id:int) -> void:
	if thread_id >= Global.GetMaxImportThreads(): return
	var status:int = get_thread_status(thread_id)
	if status > Status.PAUSED: return
	if status < Status.ACTIVE: return
	
	if status == Status.PAUSED:
		set_thread_status(thread_id, Status.ACTIVE)
	else: set_thread_status(thread_id, Status.PAUSED)

func cancel_all() -> void:
	for thread_id in thread_status.size():
		set_thread_status(thread_id, Status.CANCELED)
		
	for thread_id in thread_pool.size():
		_stop(thread_id)

func cancel_manager() -> void:
	stop_manager = true
	if manager_thread.is_active() or manager_thread.is_alive():
		manager_thread.wait_to_finish()
	stop_manager = false	

func cancel_IS_thread() -> void:
	stop_import_section = true
	if import_section_thread.is_active() or import_section_thread.is_alive():
		import_section_thread.wait_to_finish()
	stop_import_section = false

func cancel(thread_id:int) -> void:
	if thread_id >= Global.GetMaxImportThreads(): return
	if get_thread_status(thread_id) == Status.INACTIVE: return
	set_thread_status(thread_id, Status.CANCELED)
	_stop(thread_id)

func _stop(thread_id:int) -> void:
	if thread_pool[thread_id].is_active() or thread_pool[thread_id].is_alive():
		thread_pool[thread_id].wait_to_finish()
		set_thread_status(thread_id, Status.INACTIVE)
		active_threads -= 1

func shuffle_args() -> void:
	argument_mutex.lock()
	argument_queue.shuffle()
	argument_mutex.unlock()

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
	var result = null
	if thread_id < thread_args.size(): 
		result = thread_args[thread_id]
	argument_mutex.unlock()
	return result

func get_thread_args_size() -> int:
	argument_mutex.lock()
	var size:int = thread_args.size()
	argument_mutex.unlock()
	return size

func get_thread_status(thread_id:int) -> int:
	status_mutex.lock()
	var status:int = Status.ERROR
	if thread_id < thread_status.size():
		status = thread_status[thread_id]
	status_mutex.unlock()
	return status

func set_thread_status(thread_id:int, status:int) -> void:
	status_mutex.lock()
	if thread_id < thread_status.size():
		thread_status[thread_id] = status
	status_mutex.unlock()

func _manager_thread() -> void:
	manager_done = false
	var current_section = get_args()
	while not stop_manager:
		if not pause_manager:
			if current_section == null: break
			for thread_id in get_thread_args_size():
				if get_thread_args(thread_id) == null:
					set_thread_args(thread_id, current_section)
					start_one()
					current_section = get_args()
					break
		OS.delay_msec(delay_time)
	call_deferred("_manager_done")

func _manager_done() -> void:
	if manager_thread.is_active() or manager_thread.is_alive():
		manager_thread.wait_to_finish()
	manager_done = true

func _thread(thread_id:int) -> void:
	var tabs = null
	var paths = null
	while get_thread_status(thread_id) != Status.CANCELED:
		var section = get_thread_args(thread_id)
		if section == null and manager_done: break
		if get_thread_status(thread_id) != Status.PAUSED and section != null:
			var import_id:String = section[0]
			var progress_id:String = section[1]			
			ImportManager.ImportImages(import_id, progress_id)
			set_thread_args(thread_id, null)
			_add_finished_section([import_id, progress_id])
		else: OS.delay_msec(delay_time)
	call_deferred("_done", thread_id)

func _done(thread_id:int) -> void:
	_stop(thread_id)

func _add_finished_section(arr:Array) -> void:
	section_mutex.lock()
	finished_sections.append(arr)
	section_mutex.unlock()

func _get_finished_section():
	var result = null
	section_mutex.lock()
	if not finished_sections.empty():
		result = finished_sections.pop_front()
	section_mutex.unlock()
	return result

func _import_section_thread() -> void:
	while not stop_import_section:
		var temp = _get_finished_section()
		if temp != null:
			var section:Array = temp
			var import_id:String = section[0]
			var progress_id:String = section[1]
			ImportManager.CompleteImportSection(import_id, progress_id)
		OS.delay_msec(200)
