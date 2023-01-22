extends MarginContainer

onready var mutex:Mutex = Mutex.new()
onready var thread:Thread = Thread.new()

var image_hash:String = ""
var rating_queue:Array = []

func _ready() -> void:
	Signals.connect("load_metadata", self, "_load_metadata")
	Signals.connect("rating_set", self, "_on_rating_set")

func _load_metadata(im_hash:String) -> void:
	image_hash = im_hash

func _on_rating_set(rating_name:String, rating_value:int) -> void:
	if MetadataManager.IncorrectImage(image_hash): return
	mutex.lock()
	rating_queue.push_back([rating_name, rating_value])
	mutex.unlock()
	start_thread()

func start_thread() -> void:
	if thread.is_active() or thread.is_alive(): return
	thread.start(self, "_thread")

func is_empty() -> bool:
	var result:bool = false
	mutex.lock()
	if rating_queue.empty():
		result = true
	mutex.unlock()
	return result	

func get_rating() -> Array:
	var result:Array = []
	mutex.lock()
	if not rating_queue.empty():
		result = rating_queue.pop_front()
	mutex.unlock()
	return result

# now I need to implement bulk ratings; which will require me to emit a signal containing the array of selected image hashes
#	every time the current selection changes (including unselecting everything)
func _thread() -> void:
	while not is_empty():
		var rating:Array = get_rating()
		if rating.size() != 2: continue
		MetadataManager.AddRating([image_hash], rating[0], rating[1])
		OS.delay_msec(201)
	thread.call_deferred("wait_to_finish")
