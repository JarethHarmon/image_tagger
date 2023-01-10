extends Node

enum Error { OK, GENERIC, DATABASE, DICTIONARY, IO, PYTHON }
enum ImageType { JPEG, PNG, APNG, GIF, WEBP, OTHER=15, ERROR=-1}
enum TabType { DEFAULT, SIMILARITY }
enum SortSimilarity { AVERAGED, AVERAGE, DIFFERENCE, WAVELET, PERCEPTUAL }
enum Order { ASCENDING, DESCENDING }
enum Sort {
	HASH, PATH, NAME, SIZE, UPLOAD, CREATION, LAST_WRITE, LAST_EDIT, DIMENSIONS, WIDTH, HEIGHT, TAG_COUNT,
	QUALITY, APPEAL, ART_STYLE, RED, LIGHT_RED, DARK_RED, GREEN, LIGHT_GREEN, DARK_GREEN, BLUE, LIGHT_BLUE, DARK_BLUE,
	YELLOW, LIGHT_YELLOW, DARK_YELLOW, CYAN, LIGHT_CYAN, DARK_CYAN, FUCHSIA, LIGHT_FUCHSIA, DARK_FUCHSIA, LIGHT, DARK, 
	ALPHA, RANDOM
}

var current_importing_ids:Dictionary = {}
var current_importing_id:String = "" 	# stores import_id of the in-progress import (if there is one) 
var current_tab_type:int = TabType.DEFAULT
var current_tab_id:String = Global.ALL
var current_visible_tab_section:Control
var currently_importing:bool = false	# whether an import is in progress
var current_imports:Dictionary = {}		# the list of in-progress imports

var ctrl_pressed:bool = false
var shift_pressed:bool = false

func _get_program_directory() -> String: 
# note: this does not work correctly for release_debug
	if OS.is_debug_build(): 
		return ProjectSettings.globalize_path("res://").plus_file("/")
	return OS.get_executable_path().get_base_dir().plus_file("/")

func _print(string, args) -> void: print("  ", string + ": ", args)

func _input(_event:InputEvent) -> void:
	if Input.is_action_pressed("ctrl"): ctrl_pressed = true
	if Input.is_action_pressed("shift"): shift_pressed = true
	if Input.is_action_just_released("ctrl"): ctrl_pressed = false
	if Input.is_action_just_released("shift"): shift_pressed = false

func humanize(number:int) -> String:
	var num:String = String(number)
	var result:String = num[len(num)-1]
	var counter:int = 1
	for i in range(len(num)-2, -1, -1):
		if counter == 3: 
			result = result.insert(0, ",")
			counter = 0
		result = result.insert(0, num[i])
		counter += 1
	return result

func make_color() -> Color:
	return Color(clamp(randf(), 0.25, 1.0), clamp(randf(), 0.25, 1.0), clamp(randf(), 0.25, 1.0))

func make_stylebox(color:Color, bg_mult=0.3, border_mult=0.05, border:int=1) -> StyleBoxFlat:
	var sbf:StyleBoxFlat = StyleBoxFlat.new()
	sbf.set_border_width_all(border)
	if Global.GetUseColoredTagBackgrounds():
		sbf.bg_color = color * bg_mult
		sbf.border_color = color * border_mult
	else:
		sbf.bg_color = Color(0.1328, 0.1328, 0.1328)
		sbf.border_color = Color.black
	if Global.GetUseRoundedTagButtons():
		sbf.corner_detail = 8
		sbf.set_corner_radius_all(5)
	return sbf

func toggle_parent_visibility_from_children(node) -> void:
	var all_hidden:bool = true
	for child in node.get_parent().get_children():
		if child.visible: 
			all_hidden = false
			break
	if all_hidden: node.get_parent().hide()
	else: node.get_parent().show()
