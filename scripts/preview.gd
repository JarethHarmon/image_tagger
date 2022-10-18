extends Control

const smooth_pixel:Material = preload("res://shaders/SmoothPixel.tres")
#const buffer_icon:StreamTexture = preload("res://assets/buffer-01.png")
const broken_icon:StreamTexture = preload("res://assets/icon-broken.png")

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

onready var timer:Timer = $Timer
onready var buffering:HBoxContainer = $margin/hbox

enum status { INACTIVE = 0, ACTIVE, PAUSED, CANCELED }
enum a_status { PLAYING, LOADING, STOPPING }

var current_image:Texture
var current_path:String
var current_hash:String

onready var manager_thread:Thread = Thread.new()
onready var image_mutex:Mutex = Mutex.new()
onready var rating_thread:Thread = Thread.new()
onready var rating_queue_mutex:Mutex = Mutex.new()
onready var animation_mutex:Mutex = Mutex.new()

var max_threads:int = 3
var active_threads:int = 0
var _stop_threads:bool = false
var args_queue:Array = []
var thread_pool:Array = []
var thread_status:Array = []
var rating_queue:Array = []
var use_buffering_icon:bool = true

var use_animation_fps_override:bool = false
var animation_mode:bool = false
var animation_fps_override:int = 0
var animation_total_frames:int = 0
var animation_fps:int = 1
var animation_index:int = 0
var animation_min_delay:float = 0.0
var animation_size:Vector2 = Vector2.ZERO
var animation_delays:Array = []
var animation_images:Array = []
var animation_status:Dictionary = {} # playing, loading, stopping (if stop, call function that removes it from dict after quitting load in c#)

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
	Signals.connect("set_animation_info", self, "set_frames")
	Signals.connect("add_animation_texture", self, "add_animation_texture")
	Signals.connect("finish_animation", self, "remove_status")

	create_threads(max_threads)
	rating_thread.start(self, "_rating_thread")
	
	# connect to settings_loaded signal here
	_on_settings_loaded()

	Signals.connect("toggle_preview_section", self, "_toggle_preview_section")
	
	timer.connect("timeout", self, "update_animation")

func _toggle_preview_section(_visible:bool) -> void: 
	self.visible = _visible
	Globals.toggle_parent_visibility_from_children(self)
	Globals.toggle_parent_visibility_from_children(self.get_parent())

func _rating_set(rating_name:String, rating_value:int) -> void:
	# for now, I will just ensure each frame of an animation has a rating
	# in the future though, I should use current_hash instead
	#	which requires that it DEFINITELY matches the current displayed image
	# 	which might mean only showing ratings when an image has loaded, and removing 
	#	them immediately if another image is clicked; (if so then do the same for tags/paths/etc)

	# currently, ratings do not load until the image has finished loading (at least the first frame)
	# ratings are also not cleared until the new image has finished loading (after clicking a new image)
	# if you load image A, then click image B, then change the ratings before image B loads, then the 
	#	ratings will be changed on image A
	#	this behavior is not a major issue since image A is also still displayed, but I need to be 
	#	certain about whether I want this to happen, and be consistent with how this functions for all
	#	metadata (including tags/paths/imports/etc)
	if not current_image.has_meta("image_hash"): return
	var image_hash:String = current_image.get_meta("image_hash")
	append_rating([image_hash, rating_name, rating_value])

func append_rating(rating):
	rating_queue_mutex.lock()
	rating_queue.push_back(rating)
	rating_queue_mutex.unlock()

func get_rating():
	rating_queue_mutex.lock()
	var result = null
	if not rating_queue.empty():
		result = rating_queue.pop_front()
	rating_queue_mutex.unlock()
	return result

func _rating_thread() -> void:
	while not _stop_threads:
		var args = get_rating()
		if args != null:
			var image_hash:String = args[0]
			var rating_name:String = args[1]
			var rating_value:int = args[2]
			Database.AddRating(image_hash, rating_name, rating_value)
		OS.delay_msec(201)
	rating_thread.call_deferred("wait_to_finish")
	
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
	#_on_smooth_pixel_toggled(Globals.settings.use_smooth_pixel)

	create_current_image()
	resize_current_image()
	yield(get_tree(), "idle_frame")
	get_node("/root/main/preview_container/ViewportContainer/Viewport/preview_viewport/image_grid/image_0").get_texture().set_meta("image_hash", "6b5a6fef622ce6f0b6b42bceb2de405018ef65fc0aaed4b369db4cdaf8985710")

func clear_image_preview() -> void:
	for child in image_grid.get_children():
		child.texture = null
	current_image = null

func stop_threads() -> void:
	_stop_threads = true
	if manager_thread.is_active() or manager_thread.is_alive():
		manager_thread.wait_to_finish()

func create_threads(num_threads:int) -> void:
	_stop_threads = true
	for t in thread_pool.size(): 
		if thread_pool[t].is_active() or thread_pool[t].is_alive():
			thread_pool[t].wait_to_finish()
	
	thread_pool.clear()
	thread_status.clear()
	for t in num_threads:
		thread_pool.append(Thread.new())
		thread_status.append(status.INACTIVE)
		_stop_threads = false

func _load_full_image(image_hash:String, path:String, found:bool=true) -> void:	
	if current_hash == image_hash: return
	image_mutex.lock()
	current_path = path
	current_hash = image_hash

	if image_history.has(image_hash): 
		# queue code here updates the most recently clicked image (so that the least-recently viewed image is removed from history 
		#	instead of the first clicked) 
		image_queue.erase(image_hash)
		image_queue.push_back(image_hash)
		var it:ImageTexture = image_history[image_hash]
		preview.set_texture(it)
		current_image = it
		# need to call a function that gets a list of ratings instead
		Signals.emit_signal("set_rating", "Appeal", Database.GetRating(image_hash, "Appeal"))
		Signals.emit_signal("set_rating", "Quality", Database.GetRating(image_hash, "Quality"))
		Signals.emit_signal("set_rating", "Art", Database.GetRating(image_hash, "Art"))
		image_mutex.unlock()
		return
	
	for i in thread_status.size():
		if thread_status[i] == status.ACTIVE:
			thread_status[i] = status.CANCELED
	animation_mutex.lock()
	for path in animation_status:
		animation_status[path] = a_status.STOPPING
		ImageImporter.AddOrUpdateAnimationStatus(path, a_status.STOPPING)
	animation_mutex.unlock()

	remove_animations()
	image_mutex.unlock()
	
	if not found:
		var it = ImageTexture.new()
		#print(broken_icon.get_data().get_width())
		it.create_from_image(broken_icon.get_data(), 0)
		it.set_meta("image_hash", "0")
		current_image = it
		#current_image = broken_icon
		resize_current_image()
		return
	
	if use_buffering_icon: 
		buffering.show()

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

func start_one(_current_hash:String, _current_path:String, thread_id:int) -> void:
	thread_status[thread_id] = status.ACTIVE
	var actual_format:int = ImageImporter.GetActualFormat(_current_path)
	if actual_format == Globals.ImageType.APNG or actual_format == Globals.ImageType.GIF:
		animation_mutex.lock()
		animation_status[_current_path] = a_status.LOADING
		animation_mutex.unlock()
		ImageImporter.AddOrUpdateAnimationStatus(_current_path, a_status.LOADING)
	thread_pool[thread_id].start(self, "_thread", [_current_hash, _current_path, thread_id, actual_format])
	active_threads += 1

func _manager_thread() -> void:
	while not _stop_threads:
		var args:Array = _get_args()
		if args.empty(): break

		var _current_hash:String = args[0]
		var _current_path:String = args[1]
		if _current_path == "": break

		for thread_id in thread_pool.size():
			if _current_path == "": break
			if thread_status[thread_id] == status.INACTIVE:
				start_one(_current_hash, _current_path, thread_id)
				break
		OS.delay_msec(50)		
	call_deferred("_manager_done")

func _manager_done() -> void:
	if manager_thread.is_active() or manager_thread.is_alive():
		manager_thread.wait_to_finish()

# not consistent at calling FinishImport (I think)
func _thread(args:Array) -> void:
	var image_hash:String = args[0]
	var path:String = args[1]
	var thread_id:int = args[2]
	var actual_format:int = args[3]

	if _stop_threads or thread_status[thread_id] == status.CANCELED: 
		call_deferred("_done", thread_id, path)
		return
	animation_mode = false
	if actual_format == Globals.ImageType.JPG:
		var f:File = File.new()
		var e:int = f.open(path, File.READ)
		var b:PoolByteArray = f.get_buffer(f.get_len())
		f.close()
		if _stop_threads or thread_status[thread_id] == status.CANCELED: 
			call_deferred("_done", thread_id, path)
			return
		var i:Image = Image.new()
		e = i.load_jpg_from_buffer(b)
		if e != OK: 
			if _stop_threads or thread_status[thread_id] == status.CANCELED: 
				call_deferred("_done", thread_id, path)
				return 
			i = ImageImporter.LoadUnsupportedImage(path)
			if i == null or _stop_threads or thread_status[thread_id] == status.CANCELED: 
				call_deferred("_done", thread_id, path)
				return
		if _stop_threads or thread_status[thread_id] == status.CANCELED: 
			call_deferred("_done", thread_id, path)
			return
		create_current_image(thread_id, i, path, image_hash)
	elif actual_format == Globals.ImageType.PNG:
		var f:File = File.new()
		var e:int = f.open(path, File.READ)
		var b:PoolByteArray = f.get_buffer(f.get_len())
		f.close()
		if _stop_threads or thread_status[thread_id] == status.CANCELED: 
			call_deferred("_done", thread_id, path)
			return	
		var i:Image = Image.new()
		e = i.load_png_from_buffer(b)
		if e != OK: 
			print_debug(e, " :: ", path)
			if _stop_threads or thread_status[thread_id] == status.CANCELED: 
				call_deferred("_done", thread_id, path)
				return 
			i = ImageImporter.LoadUnsupportedImage(path)
			if i == null or _stop_threads or thread_status[thread_id] == status.CANCELED: 
				call_deferred("_done", thread_id, path)
				return
		if _stop_threads or thread_status[thread_id] == status.CANCELED: 
			call_deferred("_done", thread_id, path)
			return
		create_current_image(thread_id, i, path, image_hash)
	elif actual_format == Globals.ImageType.APNG: 
		animation_mode = true
		ImageImporter.LoadAPng(path, image_hash)
	elif actual_format == Globals.ImageType.GIF: 
		animation_mode = true
		ImageImporter.LoadGif(path, image_hash)
	elif actual_format == Globals.ImageType.OTHER:
		if _stop_threads or thread_status[thread_id] == status.CANCELED: 
			call_deferred("_done", thread_id, path)
			return 
		var i = ImageImporter.LoadUnsupportedImage(path)
		if i == null or _stop_threads or thread_status[thread_id] == status.CANCELED: 
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
	# this function is no longer called for animated images (resize() rewrite)
	# needs to be rewritten, but I will do so when the entire script gets rewritten
	
	if im == null:
		if animation_mode:
			im = animation_images[animation_index].get_data()
		else:
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

		# need to call a function that gets a list of ratings instead
		#	var ratings:Array = Database.GetRatings()
		#	for rating in ratings:
		#		Signals.emit_signal("set_rating", rating, Database.GetRating(image_hash, rating))
		# also need to consider doing this in bulk (which would likely require a rewrite of the rating.gd script, or the logic behind ratings in general)

		#Signals.emit_signal("set_rating", Database.GetRating(image_hash, "Default"))
		Signals.emit_signal("set_rating", "Appeal", Database.GetRating(image_hash, "Appeal"))
		Signals.emit_signal("set_rating", "Quality", Database.GetRating(image_hash, "Quality"))
		Signals.emit_signal("set_rating", "Art", Database.GetRating(image_hash, "Art"))
		
	if thread_id > 0 and path != "":
		# if check_stop_thread(thread_id):
		#	return
		pass
	current_image = it	

func change_filter() -> void:
	if preview.get_texture() == null: return
	var it:Texture
	if animation_mode: it = animation_images[animation_index]
	else: it = preview.get_texture()
	
	if Globals.settings.use_filter: it.flags = 4
	else: it.flags = 0	

func resize_current_image(path:String="") -> void:
	if current_image == null: return
	if path != "" and path != current_path: pass#return # do not remember why I commented return here
	
	# new code (still erratic and incorrect)
	#yield(get_tree(), "idle_frame")
	#var im_size:Vector2 = Vector2(current_image.get_width(), current_image.get_height())
	#Signals.emit_signal("calc_default_camera_zoom", im_size)
	#image_grid.get_parent().rect_size = im_size

	var temp_size:Vector2 = calc_size(current_image).round()
	# prevent issue causing previewed image to not change if the newly clicked image is the same size (while still preventing flash)
	if temp_size != animation_size or (current_image.get_meta("image_hash") != preview.get_texture().get_meta("image_hash")):
		animation_size = temp_size
		preview.set_texture(null)
		if current_image == null: return
		current_image.set_size_override(animation_size)
		yield(get_tree(), "idle_frame")
		if current_image == null: return
		preview.set_texture(current_image)
	buffering.hide()

# add a toggle button that inverts the calculations for portrait/landscape
# this way the user can change the size of a tall vertical image to fit horizontally on the screen
# also in general need to see if I can change to only moving the camera and not changing sizes at all 
#	(would help especially with animated images)
var invert:bool = false
func calc_size(it:ImageTexture) -> Vector2:
	var size_1:Vector2 = viewport_display.rect_size
	var size_2:Vector2 = preview.rect_size
	var size_i:Vector2 = Vector2(it.get_width(), it.get_height())
	var size:Vector2 = Vector2.ZERO
	
	if size_i == Vector2.ZERO: return size_i # prevent /0 (still need to handle images that are too large somewhere else)
	
	var ratio_h:float = size_1.y / size_i.y # causes /0 crash when image is too large (fails to load and gives size of 0)
	var ratio_w:float = size_1.x / size_i.x
	var ratio_s:Vector2 = size_2 / size_1
	
	if (ratio_h < ratio_w and not invert) or (ratio_h > ratio_w and invert): # portrait or landscape+invert
		size.y = size_1.y
		size.x = (size_1.y / size_i.y) * size_i.x
		if ratio_s.y < ratio_s.x: # portrait-shaped section
			size *= ratio_s.y
		else: size *= ratio_s.x
	else: # landscape or square or portrait+invert
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
	#_on_smooth_pixel_toggled(smooth_pixel_button.pressed)
	
	change_filter()
	#create_current_image()
	#resize_current_image()
	
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

# need to add spinbox for fps override (and connect the signals for value changed, and the signal for toggle button

# consider moving all "new" code to this function since it is only called at the start anyways
func set_frames(total_frames:int, fps:int=0) -> void:
	if fps > 0:
		animation_fps = fps
		if use_animation_fps_override: animation_min_delay = 1.0 / max(1, animation_fps_override)
		else: animation_min_delay = 1.0 / max(1, fps)
	animation_total_frames = total_frames
	# set max frames label text

func remove_animations() -> void:
	animation_mutex.lock()
	animation_delays.clear()
	animation_images.clear()
	animation_mutex.unlock()

func add_animation_texture(texture:ImageTexture, path:String, delay:float=0.0, new_image:bool=false) -> void:
	if path == current_path:
		animation_mutex.lock()
		if new_image:
			animation_delays.clear()
			animation_images.clear()
		animation_images.append(texture)
		if delay > 0.0: animation_delays.append(delay)
		if new_image:
			animation_mutex.unlock()
			update_animation(new_image)
			#update_animation(path, new_image)
		else: animation_mutex.unlock()
	else: 
		animation_mutex.lock()
		if animation_status.has(path): 
			animation_status[path] = a_status.STOPPING
			ImageImporter.AddOrUpdateAnimationStatus(path, a_status.STOPPING)
		animation_mutex.unlock()

func update_animation(new_image:bool=false) -> void:
	if not timer.is_stopped(): timer.stop()
	var path:String = current_path
	animation_mutex.lock()
	if not animation_status.has(path): 
		animation_mutex.unlock()
		return
	if animation_status[path] == a_status.STOPPING: 
		animation_mutex.unlock()
		return
	
	# set frame counter label text
	var delay:float = 0.0
	if new_image:
		animation_index = 0
		animation_size = calc_size(animation_images[animation_index]).round()
		animation_images[animation_index].set_size_override(animation_size)
		current_image = animation_images[animation_index]
		delay = animation_delays[animation_index]
		var tex:ImageTexture = animation_images[animation_index]
		if Globals.settings.use_filter: tex.flags = 4
		else: tex.flags = 0
		preview.set_texture(tex)
		animation_index = 1
		Signals.emit_signal("set_rating", "Appeal", Database.GetRating(current_hash, "Appeal"))
		Signals.emit_signal("set_rating", "Quality", Database.GetRating(current_hash, "Quality"))
		Signals.emit_signal("set_rating", "Art", Database.GetRating(current_hash, "Art"))
	else:
		if animation_index >= animation_total_frames: animation_index = 0
		if animation_images.size() > animation_index:
			animation_images[animation_index].set_size_override(animation_size)
			delay = animation_delays[animation_index]
			var tex:ImageTexture = animation_images[animation_index]
			if Globals.settings.use_filter: tex.flags = 4
			else: tex.flags = 0
			preview.set_texture(tex)
			animation_index += 1
	animation_mutex.unlock()
	timer.start(delay if delay > 0.0 else animation_min_delay)

func remove_status(path:String) -> void:
	animation_mutex.lock()
	animation_status[path] = a_status.PLAYING
	animation_mutex.unlock()

func _on_flip_h_button_up() -> void: preview.flip_h = not preview.flip_h
func _on_flip_v_button_up() -> void: preview.flip_v = not preview.flip_v
func _on_invert_size_toggled(button_pressed:bool) -> void:
	invert = button_pressed
	resize_current_image()
