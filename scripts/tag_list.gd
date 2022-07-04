extends Control

# maybe have an option button with a list of default delimiter options (comma, dot, ::, etc)

onready var tag_flow:HFlowContainer = $margin/vbox/margin/scroll/flow
onready var tag_entry:LineEdit = $margin/vbox/hbox/tag_entry
onready var delimiter_entry:LineEdit = $margin/vbox/hbox/vbox/delimiter_entry
onready var scroll:ScrollContainer = $margin/vbox/margin/scroll

var current_tags:Dictionary = {}

var use_delimiter:bool = true
var delimiter:String = ","

var use_colored_backgrounds:bool = true
var use_colored_text:bool = false

func _ready() -> void:
	tag_entry.connect("text_entered", self, "tag_entered")
	delimiter_entry.connect("text_changed", self, "delimiter_changed")

func delimiter_changed(text:String) -> void: delimiter = text if text != "" else ","

func tag_entered(text:String) -> void:
	var tags:Array = text.split(delimiter, false) as Array if use_delimiter else [text]
	for tag in tags:
		if current_tags.has(tag): continue
		current_tags[tag] = null
		var b:Button = Button.new()
		b.text = " " + tag.strip_edges() + " "
		var color:Color = Color(clamp(randf(), 0.25, 1.0), clamp(randf(), 0.25, 1.0), clamp(randf(), 0.25, 1.0), 1.0)		
		if use_colored_text: b.set("custom_colors/font_color", color * 1.5)
		if use_colored_backgrounds: b.self_modulate = color * 2.0
		tag_flow.add_child(b)
	tag_entry.text = ""	
	scroll_to_end()

func scroll_to_end() -> void:
	var scrollbar:VScrollBar = scroll.get_v_scrollbar()
	yield(get_tree(), "idle_frame")
	yield(get_tree(), "idle_frame")
	scrollbar.value = scrollbar.max_value

func clear_tag_list() -> void:
	current_tags.clear()
	for child in tag_flow.get_children():
		child.queue_free()

