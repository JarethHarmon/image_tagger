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
var use_colored_text:bool = false

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
	var color:Color = Color(clamp(randf(), 0.25, 1.0), clamp(randf(), 0.25, 1.0), clamp(randf(), 0.25, 1.0), 1.0)		
	if use_colored_text: b.set("custom_colors/font_color", color * 1.5)
	if use_colored_backgrounds: b.self_modulate = color * 2.0
	tag_flow.add_child(b)

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

