extends Control

const smooth_pixel:Material = preload("res://shaders/SmoothPixel.tres")
#const buffer_icon:StreamTexture = preload("res://assets/buffer-01.png")

export (NodePath) onready var camera = get_node(camera)
export (NodePath) onready var viewport = get_node(viewport)
export (NodePath) onready var color_grading = get_node(color_grading)
export (NodePath) onready var edge_mix = get_node(edge_mix)
export (NodePath) onready var fxaa = get_node(fxaa)
export (NodePath) onready var image_grid = get_node(image_grid)
export (NodePath) onready var preview = get_node(preview)
export (NodePath) onready var viewport_display = get_node(viewport_display)

onready var display:TextureRect = $margin/vbox/viewport_display
onready var smooth_pixel_button:CheckButton = $margin/vbox/flow/smooth_pixel
onready var filter_button:CheckButton = $margin/vbox/flow/filter
onready var fxaa_button:CheckButton = $margin/vbox/flow/fxaa
onready var edge_mix_button:CheckButton = $margin/vbox/flow/edge_mix
onready var color_grading_button:CheckButton = $margin/vbox/flow/color_grading

onready var buffering:HBoxContainer = $margin/hbox

enum status { INACTIVE = 0, ACTIVE, PAUSED, CANCELED }

var current_image:Texture
var current_path:String

onready var manager_thread:Thread = Thread.new()
onready var image_mutex:Mutex = Mutex.new()
var max_threads:int = 3
var active_threads:int = 0
var stop_threads:bool = false
var args_queue:Array = []
var thread_pool:Array = []
var thread_status:Array = []
var use_buffering_icon:bool = true

var image_history:Dictionary = {}		# image_hash:ImageTexture :: stores last N loaded full images
var image_queue:Array = []				# fifo queue of image_hash, determines contents

func _ready() -> void:
	display.texture = viewport.get_texture()
	display.color_grading = color_grading
	display.edge_mix = edge_mix
	display.fxaa = fxaa
	display.initialize(camera)
	
	Signals.connect("resize_preview_image", self, "resize_current_image")
	Signals.connect("load_full_image", self, "_load_full_image")
	Signals.connect("rating_set", self, "_rating_set")
	
	create_threads(max_threads)
	
	# connect to settings_loaded signal here
	_on_settings_loaded()

func _rating_set(rating:int) -> void:
	if not current_image.has_meta("image_hash"): return
	var image_hash:String = current_image.get_meta("image_hash")
	Database.AddRating(image_hash, "Default", rating)

func _on_settings_loaded() -> void:
	smooth_pixel_button.pressed = Globals.settings.use_smooth_pixel
	smooth_pixel_button.disabled = Globals.settings.use_filter
	filter_button.pressed = Globals.settings.use_filter
	fxaa_button.pressed = Globals.settings.use_fxaa
	color_grading_button.pressed = Globals.settings.use_color_grading
	edge_mix_button.pressed = Globals.settings.use_edge_mix
	
	_on_filter_toggled(Globals.settings.use_filter)
	_on_fxaa_toggled(Globals.settings.use_fxaa)
	_on_edge_mix_toggled(Globals.settings.use_edge_mix)
	_on_color_grading_toggled(Globals.settings.use_color_grading)
	_on_smooth_pixel_toggled(Globals.settings.use_smooth_pixel)

func clear_image_preview() -> void:
	for child in image_grid.get_children():
		child.texture = null
	current_image = null

func stop_threads() -> void:
	stop_threads = true
	if manager_thread.is_active() or manager_thread.is_alive():
		manager_thread.wait_to_finish()

func create_threads(num_threads:int) -> void:
	stop_threads = true
	for t in thread_pool.size(): 
		if thread_pool[t].is_active() or thread_pool[t].is_alive():
			thread_pool[t].wait_to_finish()
	
	thread_pool.clear()
	thread_status.clear()
	for t in num_threads:
		thread_pool.append(Thread.new())
		thread_status.append(status.INACTIVE)
	stop_threads = false

func _load_full_image(image_hash:String, path:String) -> void:	
	image_mutex.lock()

	if image_history.has(image_hash): 
		# queue code here updates the most recently clicked image (so that the least-recently viewed image is removed from history 
		#	instead of the first clicked) 
		image_queue.erase(image_hash)
		image_queue.push_back(image_hash)
		var it:ImageTexture = image_history[image_hash]
		preview.set_texture(it)
		current_image = it
		Signals.emit_signal("set_rating", Database.GetRating(image_hash, "Default"))
		image_mutex.unlock()
		return
	
	for i in thread_status.size():
		if thread_status[i] == status.ACTIVE:
			thread_status[i] = status.CANCELED
	image_mutex.unlock()
	
	if use_buffering_icon: 
		buffering.show()
		#preview.set_texture(buffer_icon)
	append_args(image_hash, path)
	start_manager()

func append_args(image_hash:String, path:String) -> void:
	image_mutex.lock()
	args_queue.push_back([image_hash, path])
	image_mutex.unlock()

func _get_args() -> Array:
	var args:Array = []
	image_mutex.lock()
	if not args_queue.empty():
		args = args_queue.pop_front()
	image_mutex.unlock()
	return args

func start_manager() -> void:
	if not manager_thread.is_active(): 
		manager_thread.start(self, "_manager_thread")

func start_one(current_hash:String, current_path:String, thread_id:int) -> void:
	thread_status[thread_id] = status.ACTIVE
	thread_pool[thread_id].start(self, "_thread", [current_hash, current_path, thread_id])
	active_threads += 1

func _manager_thread() -> void:
	var args:Array = _get_args()
	var current_hash:String = args[0]
	var current_path:String = args[1]
	var path_used:bool = false
	while not stop_threads:
		if current_path == "": break
		for thread_id in thread_pool.size():
			if current_path == "": break
			if thread_status[thread_id] == status.INACTIVE:
				start_one(current_hash, current_path, thread_id)
				path_used = true
				break
		if path_used: break
		OS.delay_msec(50)		
	call_deferred("_manager_done")

func _manager_done() -> void:
	if manager_thread.is_active() or manager_thread.is_alive():
		manager_thread.wait_to_finish()

enum formats { FAIL=-1, JPG=0, PNG, APNG, OTHER=7}

# not consistent at calling FinishImport (I think)
func _thread(args:Array) -> void:
	var image_hash:String = args[0]
	var path:String = args[1]
	var thread_id:int = args[2]

	if stop_threads or thread_status[thread_id] == status.CANCELED:
		call_deferred("_done", thread_id, path)
		return
	var actual_format:int = ImageImporter.GetActualFormat(path)
	if stop_threads or thread_status[thread_id] == status.CANCELED: 
		call_deferred("_done", thread_id, path)
		return
	if actual_format == formats.JPG:
		var f:File = File.new()
		var e:int = f.open(path, File.READ)
		var b:PoolByteArray = f.get_buffer(f.get_len())
		f.close()
		if stop_threads or thread_status[thread_id] == status.CANCELED: 
			call_deferred("_done", thread_id, path)
			return
		var i:Image = Image.new()
		e = i.load_jpg_from_buffer(b)
		if e != OK: print_debug(e, " :: ", path)
		if stop_threads or thread_status[thread_id] == status.CANCELED: 
			call_deferred("_done", thread_id, path)
			return
		create_current_image(thread_id, i, path, image_hash)
	elif actual_format == formats.PNG:
		var f:File = File.new()
		var e:int = f.open(path, File.READ)
		var b:PoolByteArray = f.get_buffer(f.get_len())
		f.close()
		if stop_threads or thread_status[thread_id] == status.CANCELED: 
			call_deferred("_done", thread_id, path)
			return	
		var i:Image = Image.new()
		e = i.load_png_from_buffer(b)
		if e != OK: print_debug(e, " :: ", path)
		if stop_threads or thread_status[thread_id] == status.CANCELED: 
			call_deferred("_done", thread_id, path)
			return
		create_current_image(thread_id, i, path, image_hash)
	else: pass
	call_deferred("_done", thread_id, path)

func _done(thread_id:int, path:String) -> void:
	_stop(thread_id)
	resize_current_image(path)

func _stop(thread_id:int) -> void:
	if thread_pool[thread_id].is_active() or thread_pool[thread_id].is_alive():
		thread_pool[thread_id].wait_to_finish()
		thread_status[thread_id] = status.INACTIVE
		active_threads -= 1	

func create_current_image(thread_id:int=-1, im:Image=null, path:String="", image_hash:String="") -> void:
	if im == null:
		var tex:Texture = preview.get_texture()
		if tex == null: return
		im = tex.get_data()
	
	var it:ImageTexture = ImageTexture.new()
	it.create_from_image(im, 4 if Globals.settings.use_filter else 0)
	
	if image_hash != "":
		it.set_meta("image_hash", image_hash)
		image_mutex.lock()		
		if image_queue.size() < Globals.settings.images_to_store:
			image_history[image_hash] = it
			image_queue.push_back(image_hash)
		else:
			var remove:String = image_queue.pop_front()
			image_history.erase(remove)
			image_history[image_hash] = it
			image_queue.push_back(image_hash)
		image_mutex.unlock()
	
	if thread_id > 0 and path != "":
		# if check_stop_thread(thread_id):
		#	return
		pass
	current_image = it
	Signals.emit_signal("set_rating", Database.GetRating(image_hash, "Default"))

func resize_current_image(path:String="") -> void:
	if current_image == null: return
	if path != "" and path != current_path: pass#return
	
	preview.set_texture(null)
	current_image.set_size_override(calc_size(current_image))
	yield(get_tree(), "idle_frame")
	preview.set_texture(current_image)
	buffering.hide()

func calc_size(it:ImageTexture) -> Vector2:
	var size_1:Vector2 = viewport_display.rect_size
	var size_2:Vector2 = preview.rect_size
	var size_i:Vector2 = Vector2(it.get_width(), it.get_height())
	var size:Vector2 = Vector2.ZERO
	
	if size_i == Vector2.ZERO: return size_i # prevent /0 (still need to handle images that are too large somewhere else)
	
	var ratio_h:float = size_1.y / size_i.y # causes /0 crash when image is too large (fails to load and gives size of 0)
	var ratio_w:float = size_1.x / size_i.x
	var ratio_s:Vector2 = size_2 / size_1
	
	if ratio_h < ratio_w: # portrait
		size.y = size_1.y
		size.x = (size_1.y / size_i.y) * size_i.x
		if ratio_s.y < ratio_s.x: # portrait-shaped section
			size *= ratio_s.y
		else: size *= ratio_s.x
	else: # landscape or square
		size.x = size_1.x
		size.y = (size_1.x / size_i.x) * size_i.y
		if ratio_s.y < ratio_s.x: size *= ratio_s.y
		else: size *= ratio_s.x
	return size


# need to update settings dictionary here as well
func _on_smooth_pixel_toggled(button_pressed:bool) -> void:
	Globals.settings.use_smooth_pixel = button_pressed
	if button_pressed and Globals.settings.use_filter: preview.set_material(smooth_pixel)
	else: preview.set_material(null)
	
func _on_filter_toggled(button_pressed:bool) -> void:
	Globals.settings.use_filter = button_pressed
	if button_pressed: smooth_pixel_button.disabled = false
	else: smooth_pixel_button.disabled = true
	_on_smooth_pixel_toggled(smooth_pixel_button.pressed)

	create_current_image()
	resize_current_image()
	
func _on_fxaa_toggled(button_pressed:bool) -> void: 
	Globals.settings.use_fxaa = button_pressed
	fxaa.visible = button_pressed
	
func _on_edge_mix_toggled(button_pressed:bool) -> void: 
	Globals.settings.use_edge_mix = button_pressed
	edge_mix.visible = button_pressed
	
func _on_color_grading_toggled(button_pressed:bool) -> void: 
	Globals.settings.use_color_grading = button_pressed
	color_grading.visible = button_pressed

export (NodePath) onready var normal_parent = get_node(normal_parent)
export (NodePath) onready var fullscreen_parent = get_node(fullscreen_parent)
onready var fullscreen_button:Button = $margin/vbox/flow/fullscreen
onready var darker_background:ColorRect = $darker_background
var fullscreen:bool = false

func _input(event:InputEvent) -> void:
	if Input.is_action_just_pressed("fullscreen"): _on_fullscreen_pressed()
	if Input.is_action_just_pressed("ui_cancel"): _on_fullscreen_pressed(true)

func _on_fullscreen_pressed(escape:bool=false) -> void:
	if escape and not fullscreen: return
	if fullscreen: 
		fullscreen_button.text = "  [[  ]]  "
		fullscreen_parent.remove_child(self)
		normal_parent.add_child(self)
		normal_parent.move_child(self, 0)
		darker_background.hide()
		fullscreen = false
	else:
		fullscreen_button.text = "    []    "
		normal_parent.remove_child(self)
		fullscreen_parent.add_child(self)
		darker_background.show()
		fullscreen = true
