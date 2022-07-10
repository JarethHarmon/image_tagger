extends Control

# this script only handles scanning the directories
# it will call the relevant c# functions to do so
# then it will present a list of scanned directories to the user and allow them
# to filter by image size, remove folders/paths, scan additional folders/paths, filter
# by file extension, etc (which will all be done on another script)

onready var scan_thread:Thread = Thread.new()
onready var scan_mutex:Mutex = Mutex.new()

var scan_queue:Array = []
var scanner_active:bool = false 

# needs to take into accounr a blacklist of folders (can be stored on c# side though)
func queue_append(scan_folder:String, recursive:bool=true) -> void:
	scan_queue.append([scan_folder, recursive])
	if !scanner_active: start_scanner()

# protecting scan_queue with a mutex is not necessary since the scanner will only be single threaded
# (it finishes even large folders in a few milliseconds)
func start_scanner() -> void:
	if scanner_active: return
	if scan_mutex.try_lock() != OK: return
	if scan_thread.is_alive(): return
	scan_mutex.lock()
	scan_thread.start(self, "_thread")
	scanner_active = true

func _thread() -> void:
	while not scan_queue.empty():
		var scan:Array = scan_queue.pop_front()
		var image_count:int = ImageScanner.ScanDirectories(scan[0], scan[1])
		# there will only every be one scan im progress/waiting to import at a time
		# need to store the count and the array of found paths globally somewhere
		# need to display array of found paths to user (in item list)
		# need to allow user to remove paths/folders, filter by image size/date, etc
		# user should be able to find and scan additional folders while the first set is still scanning
		# once import process starts, user should be able to scan and begin importing new folders while it is running
		OS.delay_msec(50)
	call_deferred("_done")

func _done() -> void:
	if scan_thread.is_active() or scan_thread.is_alive():
		scan_thread.wait_to_finish()
	scanner_active = false
	scan_mutex.unlock()
	print("scan finished")

