extends Control

# maybe have an option button with a list of default delimiter options (comma, dot, ::, etc)

onready var tag_flow:HFlowContainer = $margin/vbox/margin/scroll/flow
onready var tag_entry:LineEdit = $margin/vbox/hbox/tag_entry
onready var delimiter_entry:LineEdit = $margin/vbox/hbox/vbox/delimiter_entry
onready var scroll:ScrollContainer = $margin/vbox/margin/scroll

var current_tags:Dictionary = {}
var selected_thumbnails:Dictionary = {}

var use_delimiter:bool = true
var delimiter:String = ","
var curr_hash:String = ""

var use_colored_backgrounds:bool = true
var use_colored_text:bool = true
var use_different_colors:bool = false
var use_rounded_buttons:bool = true

func _ready() -> void:
	tag_entry.connect("text_entered", self, "tag_entered")
	delimiter_entry.connect("text_changed", self, "delimiter_changed")
	Signals.connect("load_image_tags", self, "_load_tags")

func _load_tags(image_hash:String, selection:Dictionary) -> void:
	clear_tag_list()
	curr_hash = image_hash
	var tags:Array = Database.GetTags(image_hash)
	#print_debug(tags)
	for tag in tags: add_tag(tag)
	selected_thumbnails = selection

func delimiter_changed(text:String) -> void: delimiter = text if text != "" else ","

func tag_entered(text:String) -> void:
	if text == "": return
	if curr_hash == "": 
		tag_entry.text = ""	
		return
	var tags:Array = text.split(delimiter, false) as Array if use_delimiter else [text]
	for tag in tags:
		add_tag(tag)
	tag_entry.text = ""	
	scroll_to_end()
	upload_tags(tags, selected_thumbnails)

func add_tag(tag:String) -> void:
	if current_tags.has(tag): return
	current_tags[tag] = null
	var b:Button = Button.new()
	b.text = "  " + tag.strip_edges() + "  "
	var color:Color = make_color()
	if use_colored_text:
		if use_different_colors:
			var color2:Color = make_color()
			b.set("custom_colors/font_color", color2 * 1.5)	
		else: b.set("custom_colors/font_color", color * 1.5)
	var sbf:StyleBoxFlat = make_stylebox(color)
	b.add_stylebox_override("normal", sbf)
	b.add_stylebox_override("hover", make_stylebox(color, 0.05, 5.0))
	sbf = make_stylebox(color, 2.0, 0.05, 2)
	b.add_stylebox_override("pressed", sbf)
	b.add_stylebox_override("focus", sbf)
	b.add_color_override("font_color_pressed", Color.black)
	b.add_color_override("font_color_focus", Color.black)
	tag_flow.add_child(b)

func make_color() -> Color:
	return Color(clamp(randf(), 0.25, 1.0), clamp(randf(), 0.25, 1.0), clamp(randf(), 0.25, 1.0))

func make_stylebox(color:Color, bg_mult=0.3, border_mult=0.05, border:int=1) -> StyleBoxFlat:
	var sbf:StyleBoxFlat = StyleBoxFlat.new()
	sbf.set_border_width_all(border)
	if use_colored_backgrounds: 
		sbf.bg_color = color * bg_mult
		sbf.border_color = color * border_mult
	else:
		sbf.bg_color = Color(0.1328, 0.1328, 0.1328)
		sbf.border_color = Color.black
	if use_rounded_buttons:
		sbf.corner_detail = 8
		sbf.set_corner_radius_all(5)
	return sbf

func upload_tags(tags:Array, selection:Dictionary) -> void:
	var selected_hashes:Array = []
	for idx in selection: selected_hashes.push_back(selection[idx])
	var thread:Thread = Thread.new()
	thread.start(self, "_thread", [thread, tags, selected_hashes])

func _thread(args:Array) -> void:
	var thread:Thread = args[0]
	var tags:Array = args[1]
	var selected_hashes:Array = args[2]
	#print_debug(tags)
	Database.BulkAddTags(selected_hashes, tags)
	# still need to actually create the tags in the tags database (to store meta info about the tags themselves)
	# current tags are not complex enough for this to actually be necessary though
	thread.call_deferred("wait_to_finish")

func scroll_to_end() -> void:
	var scrollbar:VScrollBar = scroll.get_v_scrollbar()
	yield(get_tree(), "idle_frame")
	yield(get_tree(), "idle_frame")
	scrollbar.value = scrollbar.max_value

func clear_tag_list() -> void:
	current_tags.clear()
	for child in tag_flow.get_children():
		child.queue_free()

