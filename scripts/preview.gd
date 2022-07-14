extends Control

const smooth_pixel:Material = preload("res://shaders/SmoothPixel.tres")

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

var current_image:Texture
var current_path:String

onready var image_mutex:Mutex = Mutex.new()
var max_threads:int = 3
var stop_threads:bool = false
var thread_queue:Array = []
var thread_active:Array = []

func _ready() -> void:
	display.texture = viewport.get_texture()
	display.color_grading = color_grading
	display.edge_mix = edge_mix
	display.fxaa = fxaa
	display.initialize(camera)
	
	Signals.connect("resize_preview_image", self, "resize_current_image")
	Signals.connect("load_full_image", self, "_load_full_image")
	
	create_threads(max_threads)
	
	# connect to settings_loaded signal here
	_on_settings_loaded()
	
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

func create_threads(num_threads:int) -> void:
	stop_threads = true
	for t in thread_queue.size(): 
		if thread_queue[t].is_active() or thread_queue[t].is_alive():
			thread_queue[t].wait_to_finish()
	thread_queue.clear()
	thread_active.clear()
	for t in num_threads:
		thread_queue.append(Thread.new())
		thread_active.append(false)

func _load_full_image(path:String) -> void:
	pass

func create_current_image(thread_id:int=-1, im:Image=null, path:String="") -> void:
	if im == null:
		var tex:Texture = preview.get_texture()
		if tex == null: return
		im = tex.get_data()
	
	var it:ImageTexture = ImageTexture.new()
	it.create_from_image(im, 4 if Globals.settings.use_filter else 0)
	
	if thread_id > 0 and path != "":
		# if check_stop_thread(thread_id):
		#	return
		pass
	current_image = it

func resize_current_image(path:String="") -> void:
	if current_image == null: return
	if path != "" and path != current_path: pass#return
	
	preview.set_texture(null)
	current_image.set_size_override(calc_size(current_image))
	yield(get_tree(), "idle_frame")
	preview.set_texture(current_image)

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
	


