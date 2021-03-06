extends TextureRect

var camera:Camera2D

var color_grading:Control
var edge_mix:Control
var fxaa:Control

var default_camera_position:Vector2
var default_camera_zoom:Vector2
var default_camera_offset:Vector2 

func initialize(cam:Camera2D) -> void:
	camera = cam
	default_camera_position = camera.position
	default_camera_zoom = camera.zoom
	default_camera_offset = camera.offset
	
# zoom
var zoom_to_point:bool = true
var zoom_in_max:float = 0.025
var zoom_out_max:float = 4.0
var zoom_step:float = 0.05

# scroll
var scroll_step:float = 15.0
var scroll_speed:float = 4.0
var scroll_weight:float = 0.3

# drag
var dragging:bool = false
var drag_speed:float = 1.1
var drag_step:float = 0.4

# shaders : :color_grade
var use_colorgrade:bool = false
var colorgrade_falloff:float = 0.0
var colorgrade_high:Color = Color.black
var color_grade_low:Color = Color.white

# shaders :: edge_mix
var use_edge_default_motion_mix:bool = false
var edmm_line_size:float = 0.0
var edmm_threshold:float = 0.0
var edmm_line_weight:float = 0.0
var edmm_graduation_size:float = 2.5
var edmm_weight:float = 0.5

func zoom_point(amount:float, position:Vector2) -> void:
	var prev_zoom:Vector2 = camera.zoom
	camera.zoom += camera.zoom * amount
	camera.offset += (((self.rect_position + self.rect_size) * 0.5) - position) * (camera.zoom - prev_zoom)

func _on_viewport_display_gui_input(event:InputEvent) -> void:
	if event is InputEventMouseButton:
		if event.button_index == BUTTON_WHEEL_UP:
			if Input.is_action_pressed("ctrl"): # scroll up
				camera.offset.y = lerp(camera.offset.y, camera.offset.y - scroll_step * scroll_speed * camera.zoom.y, scroll_weight)
				#camera.offset.y -= 15
			else: # zoom in
				if camera.zoom > Vector2(zoom_in_max, zoom_in_max):
					if zoom_to_point: zoom_point(-zoom_step, event.position)
					else: camera.zoom -= Vector2(zoom_step, zoom_step)
		elif event.button_index == BUTTON_WHEEL_DOWN:
			if Input.is_action_pressed("ctrl"): # scroll down
				camera.offset.y = lerp(camera.offset.y, camera.offset.y + scroll_step * scroll_speed * camera.zoom.y, scroll_weight)
				#camera.offset.y += 15
			else: # zoom out
				if camera.zoom < Vector2(zoom_out_max, zoom_out_max):
					if zoom_to_point: zoom_point(zoom_step, event.position)
					else: camera.zoom += Vector2(zoom_step, zoom_step) # make lerp ?
		elif event.button_index == BUTTON_RIGHT: # reset
			camera.position = default_camera_position
			camera.zoom = default_camera_zoom
			camera.offset = default_camera_offset
			camera.rotation_degrees = 0 
			rotation.value = 0
			rotation_entry.value = 0
			Signals.emit_signal("resize_preview_image")
		else: # dragging
			if event.is_pressed(): dragging = true
			else: dragging = false
	elif event is InputEventMouseMotion and dragging: # dragging
		var rot = deg2rad(camera.rotation_degrees)
		var sin_rot = sin(rot) ; var cos_rot = cos(rot)
		# ensures that dragging works correctly when the camera is rotated
		var rot_mult:Vector2 = Vector2((cos_rot * event.relative.x) - (sin_rot * event.relative.y), (sin_rot * event.relative.x) + (cos_rot * event.relative.y))
		camera.position -= rot_mult * camera.zoom * drag_speed
		camera.position = lerp(camera.position, camera.position - (rot_mult * camera.zoom * drag_speed), drag_step)

onready var rotation:HSlider = get_parent().get_node("flow/rotation")
onready var rotation_entry:SpinBox = get_parent().get_node("flow/rotation_entry")

func _on_rotation_value_changed(value:int) -> void:
	camera.rotation_degrees = value
	rotation_entry.value = value	
func _on_rotation_entry_value_changed(value:int) -> void:
	camera.rotation_degrees = value
	rotation.value = value

