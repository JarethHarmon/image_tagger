extends Control

onready var button_list:HBoxContainer = $margin/hbox/scroll/button_list

onready var import_thread:Thread = Thread.new()
onready var import_mutex:Mutex = Mutex.new()

var import_queue:Array = []
var buttons:Dictionary = {}

var importer_active:bool = false

func _ready() -> void:
	# need to have import get an import_id from ImageImporter 
	# then pass that and the count along with the signal
	# then create the button here
	Signals.connect("new_import_started", self, "create_import_button")

func _on_all_button_button_up() -> void: Signals.emit_signal("group_button_pressed", "All")
func _on_group_button_pressed(import_id:String) -> void: Signals.emit_signal("group_button_pressed", import_id)

func create_import_button(import_id:String, count:int) -> void:
	if count <= 0: return
	var b:Button = Button.new()
	b.text = "  Import (0, 0/" + String(count) + ")  " 
	b.connect("button_up", self, "_on_group_button_pressed", [import_id])
	button_list.add_child(b)
	buttons[import_id] = b
	queue_append(import_id, count)

func update_button_text(import_id:String, success_count:int, total_count:int, import_name="Import") -> void:
	buttons[import_id].text = "  %s (%d, %d/%d)  " % [import_name, success_count, success_count, total_count]


# need to consider the use of multiple import threads
func queue_append(import_id:String, count:int) -> void:
	if import_id == "": return
	if import_queue.has(import_id): return
	import_queue.push_back([import_id, count])
	if !importer_active: start_importer()

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
		ImageImporter.ImportImages(import_id, count)
		OS.delay_msec(50)
	call_deferred("_done")

func _done() -> void:
	if import_thread.is_active() or import_thread.is_alive():
		import_thread.wait_to_finish()
	importer_active = false
	import_mutex.unlock()
	print("import finished")

