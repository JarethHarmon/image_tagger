extends Control

onready var button_list:HBoxContainer = $margin/hbox/scroll/button_list

onready var import_thread:Thread = Thread.new()
onready var import_mutex:Mutex = Mutex.new()

onready var all_button:Button = $margin/hbox/all_button

var import_queue:Array = []
var buttons:Dictionary = {}

var importer_active:bool = false
var thread_queue:Array = []
var thread_active:Array = []
var active_threads:int = 0
var num_threads:int = 3

func _ready() -> void:
	# need to have import get an import_id from ImageImporter 
	# then pass that and the count along with the signal
	# then create the button here
	Signals.connect("new_import_started", self, "create_new_import_button")
	Signals.connect("import_info_load_finished", self, "create_import_buttons")
	Signals.connect("update_import_button", self, "update_button_text")
	buttons["All"] = all_button
	
	for i in num_threads: 
		thread_queue.push_back(Thread.new())
		thread_active.push_back(false)

func _on_all_button_button_up() -> void: Signals.emit_signal("group_button_pressed", "All")
func _on_group_button_pressed(import_id:String) -> void: Signals.emit_signal("group_button_pressed", import_id)

func create_import_buttons() -> void: 
	var import_ids:Array = Database.GetAllImportIds()
	#print(import_ids)
	update_button_text("All", true, Database.GetImportSuccessCount("All"), Database.GetTotalCount("All"), "All")
	for import_id in import_ids:
		create_import_button(import_id, Database.GetFinished(import_id), Database.GetTotalCount(import_id), Database.GetImportSuccessCount(import_id), Database.GetImportName(import_id))

func create_import_button(import_id:String, finished:bool, total_count:int, success_count:int, import_name:String) -> void:
	if import_id == "All": return
	if total_count <= 0: return # remove it from import database (check on c# side when loading though)
	var b:Button = Button.new()
	b.text = "  " + import_name + " (" + String(success_count) + (")  " if finished else (", " + String(success_count) + "/" + String(total_count) + ")  "))
	b.connect("button_up", self, "_on_group_button_pressed", [import_id])
	button_list.add_child(b)
	buttons[import_id] = b
	
func create_new_import_button(import_id:String, count:int) -> void:
	if count <= 0: return
	var b:Button = Button.new()
	b.text = "  Import (0, 0/" + String(count) + ")  " 
	b.connect("button_up", self, "_on_group_button_pressed", [import_id])
	button_list.add_child(b)
	buttons[import_id] = b
	queue_append(import_id, count)

func update_button_text(import_id:String, finished:bool, success_count:int, total_count:int, import_name="Import") -> void:
	if not finished: buttons[import_id].text = "  %s (%d, %d/%d)  " % [import_name, success_count, success_count, total_count]
	else: buttons[import_id].text = "  %s (%d)  " % [import_name, success_count]

# need to consider the use of multiple import threads
func queue_append(import_id:String, count:int) -> void:
	if import_id == "": return
	if import_queue.has(import_id): return
	import_queue.push_back([import_id, count])
	#if !importer_active: start_importer()
	#if !importer_active: 
	start_importer_m()

func start_importer_m() -> void:
	if active_threads == num_threads: return
	for t in thread_queue.size():
		if not thread_active[t]:
			thread_queue[t].start(self, "_thread_m", t)

func _thread_m(thread_id:int) -> void:
	thread_active[thread_id] = true
	active_threads += 1
	while true:
		import_mutex.lock()
		if import_queue.empty():
			import_mutex.unlock()
			break
		var args:Array = import_queue.pop_front()
		import_mutex.unlock()
		var import_id:String = args[0]
		var count:int = args[1]
		Globals.current_importing_ids[import_id] = null
		ImageImporter.ImportImages(import_id, count) # may not be thread safe
		Globals.current_importing_ids.erase(import_id)
		OS.delay_msec(50)
	call_deferred("_done_m", thread_id)

func _done_m(thread_id:int) -> void:
	if thread_queue[thread_id].is_active() or thread_queue[thread_id].is_alive():
		thread_queue[thread_id].wait_to_finish()
	active_threads -= 1
	thread_active[thread_id] = false

func start_importer() -> void:
	if importer_active: return
	if import_mutex.try_lock() != OK: return
	if import_thread.is_alive(): return
	import_mutex.lock()
	import_thread.start(self, "_thread")
	importer_active = true

func _thread() -> void:
	while not import_queue.empty():
		var args:Array = import_queue.pop_front()
		var import_id:String = args[0]
		var count:int = args[1]
		Globals.current_importing_id = import_id
		ImageImporter.ImportImages(import_id, count)
		Globals.current_importing_id = ""
		OS.delay_msec(50)
	call_deferred("_done")

func _done() -> void:
	if import_thread.is_active() or import_thread.is_alive():
		import_thread.wait_to_finish()
	importer_active = false
	import_mutex.unlock()
	print("import finished")

