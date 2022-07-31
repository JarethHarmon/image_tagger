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


enum SortBy { FileHash, FilePath, FileSize, FileCreationUtc, FileUploadUtc, Dimensions, TagCount, Random, DefaultRating }
enum OrderBy { Ascending=0, Descending=1 }
enum ImageType { JPG=0, PNG, APNG, OTHER=7, FAIL=-1}
enum Tab { IMPORT_GROUP, IMAGE_GROUP, TAG, SIMILARITY }

var current_importing_ids:Dictionary = {}
var current_importing_id:String = "" 	# stores import_id of the in-progress import (if there is one) 
var current_tab_type:int = Tab.IMPORT_GROUP
var current_tab_id:String = "All"

var currently_importing:bool = false	# whether an import is in progress
var current_imports:Dictionary = {}		# the list of in-progress imports

var ctrl_pressed:bool = false
var shift_pressed:bool = false

var settings_path:String = "user://settings.tres"

var last_settings:Array = []
var settings:Dictionary = {
  # Paths
	"use_default_metadata_path" : true,
	"use_default_thumbnail_path" : true,
	"default_thumbnail_path" : "",
	"default_metadata_path" : "",
	"thumbnail_path" : "",
	"metadata_path" : "",
	"last_viewed_directory" : "",
	"last_imported_directory" : "",

  # Sorting
	"current_sort" : SortBy.FileHash,
	"current_order" : OrderBy.Ascending,

  # Import
	"use_recursion" : false,
	"max_bytes_to_check_apng" : 256,
	"max_import_threads" : 3,

  # Thumbnails
	"images_per_page" : 400,
	"load_threads" : 3,
	"pages_to_store" : 500,
	"max_loaded_thumbnails" : 10000,

  # Images
	"images_to_store" : 10,

  # Shaders
	"use_smooth_pixel" : true,
	"use_filter" : false,
	"use_color_grading" : true,
	"use_fxaa" : false,
	"use_edge_mix" : false,

  # UI
	"hsplit_offset" : -175,
	"left_offset" : -160,
	"right_offset" : 240,
	"use_fullscreen" : false,
	"use_colored_backgrounds" : true,
	"use_rounded_buttons" : true,
	"show_thumbnail_tooltips" : true,
}

func _print(string, args) -> void: print("  ", string + ": ", args)

func _input(event:InputEvent) -> void:
	if Input.is_action_pressed("ctrl"): ctrl_pressed = true
	if Input.is_action_pressed("shift"): shift_pressed = true
	if Input.is_action_just_released("ctrl"): ctrl_pressed = false
	if Input.is_action_just_released("shift"): shift_pressed = false

func _ready() -> void:
	settings.default_metadata_path = ProjectSettings.globalize_path("user://metadata/")
	settings.default_thumbnail_path = ProjectSettings.globalize_path("user://metadata/thumbnails/")
	load_settings()
	if settings.thumbnail_path == "": settings.thumbnail_path = settings.default_thumbnail_path
	if settings.metadata_path == "": settings.metadata_path = settings.default_metadata_path
	Signals.call_deferred("emit_signal", "settings_loaded")

func load_settings() -> void:
	var f:File = File.new()
	var e:int = f.open(settings_path, File.READ)
	if e == OK:
		var temp_settings:Dictionary = str2var(f.get_as_text())
		last_settings = create_settings_comparison_array(temp_settings)
		# this is to prevent overwriting/removing newly added settings when loading the settings file 
		# (ie future-proofing against updates that add new settings)
		for setting in temp_settings.keys():
			if settings.has(setting):
				settings[setting] = temp_settings[setting]
	f.close()
	Storage.SetMaxStoredPages(settings.pages_to_store)

func create_settings_comparison_array(settings_dict:Dictionary) -> Array:
	var result:Array = []
	var temp:Array = settings_dict.keys()
	temp.sort()
	for setting in temp: 
		result.append(setting)
		result.append(settings_dict[setting])
	return result

func save_settings() -> void:
	if (create_settings_comparison_array(settings) == last_settings): return	# don't waste time if no changes made since settings were loaded

	var f:File = File.new()
	var e:int = f.open(settings_path, File.WRITE)
	if e == OK: f.store_string(var2str(settings))
	f.close()

func get_komi_hash(path:String) -> String: 
	var gob:Gob = Gob.new()
	var komi:String = gob.get_komi_hash(path)
	gob.queue_free()
	return komi

func get_sha512(path:String) -> String:
	var gob:Gob = Gob.new()
	var sha512:String = gob.get_sha512_hash(path)
	gob.queue_free()
	return sha512

func get_sha256(path:String) -> String: return File.new().get_sha256(path)

func get_file_name(path:String) -> String: 
	var file:String = path.get_file()
	return file.rstrip("." + file.get_extension())

func is_apng(path:String) -> bool:
	var f:File = File.new()
	var e:int = f.open(path, File.READ)
	if e != OK: return false
	var p:PoolByteArray = f.get_buffer(settings.max_bytes_to_check_apng)
	f.close()
	if "6163544c" in p.hex_encode(): return true
	return false

func make_color() -> Color:
	return Color(clamp(randf(), 0.25, 1.0), clamp(randf(), 0.25, 1.0), clamp(randf(), 0.25, 1.0))

func make_stylebox(color:Color, bg_mult=0.3, border_mult=0.05, border:int=1) -> StyleBoxFlat:
	var sbf:StyleBoxFlat = StyleBoxFlat.new()
	sbf.set_border_width_all(border)
	if settings.use_colored_backgrounds: 
		sbf.bg_color = color * bg_mult
		sbf.border_color = color * border_mult
	else:
		sbf.bg_color = Color(0.1328, 0.1328, 0.1328)
		sbf.border_color = Color.black
	if settings.use_rounded_buttons:
		sbf.corner_detail = 8
		sbf.set_corner_radius_all(5)
	return sbf

