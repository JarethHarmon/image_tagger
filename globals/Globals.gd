extends Node

# All : all images that have been imported into the program
# ImportGroup : all images related to the chosen import tab (images that were imported at the same time)
# ImageGroup : images that have been associated with each other by the user (pages of a comic for example)

# IMPORTING:
#	o starts from 'import' node
#	o scans the given folders for images matching the users (optional) specifications
#	o when the user presses 'Begin Import' , create a new import button and set its text :  ' Import (success, success/found) ' ie  ' Import (35, 35/781)
#	o if the user clicks the import button it should show the numbers for : found, success, duplicate, ignored, failed  ; and should load the successful thumbnails (maybe re-query whenever one finishes importing)
#	o once the import finishes it should update the button to ' Import (success) ' ; but clicking on it would still show the full counts (success/fail/etc)


enum SortBy { FileHash, FilePath, FileName, FileSize, FileUploadUtc, FileCreationUtc, FileEditTime, Dimensions, TagCount, Random, ImageColor, RatingQuality, RatingAppeal, RatingArtStyle, RatingSum, RatingAverage, RED, GREEN, BLUE, ALPHA, LIGHT, DARK }
enum OrderBy { Ascending=0, Descending=1 }
enum ImageType { JPG=0, PNG, APNG, GIF, WEBP, OTHER=7, FAIL=-1}
enum Tab { IMPORT_GROUP, IMAGE_GROUP, TAG, SIMILARITY }
enum Similarity { AVERAGED, AVERAGE, DIFFERENCE, WAVELET }

var current_importing_ids:Dictionary = {}
var current_importing_id:String = "" 	# stores import_id of the in-progress import (if there is one) 
var current_tab_type:int = Tab.IMPORT_GROUP
var current_tab_id:String = "All"
var current_similarity:int = Similarity.AVERAGE

var current_visible_tab_section:Control

var currently_importing:bool = false	# whether an import is in progress
var current_imports:Dictionary = {}		# the list of in-progress imports

var ctrl_pressed:bool = false
var shift_pressed:bool = false

# note: this does not work correctly for release_debug
func _get_program_directory() -> String: 
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
	#if settings.use_colored_backgrounds: 
	if Global.Settings.UseColoredTagBackgrounds:
		sbf.bg_color = color * bg_mult
		sbf.border_color = color * border_mult
	else:
		sbf.bg_color = Color(0.1328, 0.1328, 0.1328)
		sbf.border_color = Color.black
	#if settings.use_rounded_buttons:
	if Global.Settings.UseRoundedTagButtons:
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
