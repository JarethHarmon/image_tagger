extends Control

# maybe have an option button with a list of default delimiter options (comma, dot, ::, etc)

onready var tag_flow:HFlowContainer = $margin/hbox/vbox1/margin/scroll/bg/flow
onready var tag_entry:LineEdit = $margin/hbox/vbox1/hbox/tag_entry
onready var delimiter_entry:LineEdit = $margin/hbox/vbox1/hbox/vbox/delimiter_entry
onready var scroll:ScrollContainer = $margin/hbox/vbox1/margin/scroll

var current_tags:Dictionary = {}		# tag : [index, button, color]
var tags_array:Array = []
var selected_thumbnails:Dictionary = {}

var use_delimiter:bool = true
var delimiter:String = ","
var curr_hash:String = ""

var use_colored_text:bool = true
var use_different_colors:bool = false

func _ready() -> void:
	tag_entry.connect("text_entered", self, "tag_entered")
	delimiter_entry.connect("text_changed", self, "delimiter_changed")
	Signals.connect("load_image_tags", self, "_load_tags")
	Signals.connect("all_selected_items", self, "set_selection")
	Signals.connect("tab_button_pressed", self, "clear_selection")
	Signals.connect("toggle_tag_section", self, "_toggle_tag_section")
	Globals.current_visible_tab_section = self

func _toggle_tag_section(_visible:bool) -> void: 
	if not Globals.current_visible_tab_section == self: return
	self.visible = _visible
	if Globals.all_siblings_hidden(self): get_parent().hide()
	else: get_parent().show()

func set_selection(selection:Dictionary) -> void:
	selected_thumbnails = selection

func _load_tags(image_hash:String, selection:Dictionary) -> void:
	clear_tag_list()
	curr_hash = image_hash
	var tags:Array = Database.GetTags(image_hash)
	for tag in tags: add_tag(tag)
	selected_thumbnails = selection

func delimiter_changed(text:String) -> void: delimiter = text if text != "" else ","

func tag_entered(text:String) -> void:
	if text == "": return
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
	var color:Color = Globals.make_color()
	if use_colored_text:
		if use_different_colors:
			var color2:Color = Globals.make_color()
			b.set("custom_colors/font_color", color2 * 1.5)	
		else: b.set("custom_colors/font_color", color * 1.5)
	var sbf:StyleBoxFlat = Globals.make_stylebox(color)
	b.add_stylebox_override("normal", sbf)
	var sbf2:StyleBoxFlat = Globals.make_stylebox(color, 2.0, 0.05, 2)
	b.add_stylebox_override("pressed", sbf2)
	b.add_stylebox_override("focus", sbf2)
	b.add_stylebox_override("hover", sbf2)
	b.add_color_override("font_color_pressed", Color.black)
	b.add_color_override("font_color_focus", Color.black)
	b.add_color_override("font_color_hover", Color.black)
	b.connect("button_up", self, "tag_clicked", [tag])
	current_tags[tag] = {
		"color" : color,
		"normal" : sbf,
		"focus" : sbf2,
		"button" : b,
	}
	tag_flow.add_child(b)
	tags_array.push_back(tag)

func upload_tags(tags:Array, selection:Dictionary) -> void:
	var selected_hashes:Array = []
	for idx in selection: selected_hashes.push_back(selection[idx])
	if selected_hashes.empty(): return
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

func clear_selection(_tab_id:String="") -> void:
	selected_thumbnails.clear()
	clear_tag_list()

func clear_tag_list() -> void:
	current_tags.clear()
	tags_array.clear()
	selected_tags.clear()
	for child in tag_flow.get_children():
		child.queue_free()

var selected_tags:Dictionary = {}
var last_clicked_index:int = -1

func tag_clicked(tag:String) -> void:
	var button:Button = current_tags[tag].button
	var index:int = button.get_index()
	if Globals.ctrl_pressed and Globals.shift_pressed: pass
	elif Globals.ctrl_pressed: 
		invert_tag_selection(tag)		
	elif Globals.shift_pressed:
		if last_clicked_index < 0: select(tag)
		elif last_clicked_index == index: invert_tag_selection(tag)
		else:
			var range_low:int = min(last_clicked_index, index)
			var range_high:int = max(last_clicked_index, index)
			var selecting:bool = not selected_tags.has(tags_array[index])
			
			for i in range(range_low, range_high+1):
				var curr_tag:String = tags_array[i]
				if selecting:
					if selected_tags.has(curr_tag) and i != last_clicked_index: deselect(curr_tag)
					else: select(curr_tag)
				else: # deselecting
					if selected_tags.has(curr_tag): deselect(curr_tag)
					else: select(curr_tag)
					# need to find a way to merge this with above logic
					if i == last_clicked_index: deselect(curr_tag)
					if i == index: select(curr_tag)
	else:
		deselect_all()
		selected_tags[tag] = button
		current_tags[tag].button.add_stylebox_override("normal", current_tags[tag].focus)
		current_tags[tag].button.add_color_override("font_color", Color.black)
	last_clicked_index = index
	button.release_focus()

func invert_tag_selection(tag:String) -> void:
	if selected_tags.has(tag): deselect(tag)
	else: select(tag)

func select(tag:String) -> void:
	current_tags[tag].button.add_stylebox_override("normal", current_tags[tag].focus)
	current_tags[tag].button.add_color_override("font_color", Color.black)
	selected_tags[tag] = null
	
func deselect(tag:String) -> void:
	current_tags[tag].button.add_stylebox_override("normal", current_tags[tag].normal)
	if use_colored_text:
		current_tags[tag].button.add_color_override("font_color", current_tags[tag].color * 1.5)
	else:
		current_tags[tag].button.remove_color_override("font_color")
	selected_tags.erase(tag)

func deselect_all() -> void:
	for tag in selected_tags: 
		current_tags[tag].button.add_stylebox_override("normal", current_tags[tag].normal)
		if use_colored_text:
			current_tags[tag].button.add_color_override("font_color", current_tags[tag].color * 1.5)
		else:
			current_tags[tag].button.remove_color_override("font_color")
	selected_tags.clear()

func _on_remove_tags_button_up() -> void:
	var tags:Array = selected_tags.keys()
	for tag in tags:
		selected_tags.erase(tag)
		tags_array.erase(tag)
		current_tags[tag].button.queue_free()
		current_tags.erase(tag)
	var hashes:Array = []
	for idx in selected_thumbnails: 
		hashes.push_back(selected_thumbnails[idx])
	Database.BulkRemoveTags(hashes, tags)

func _on_bg_gui_input(event:InputEvent) -> void:
	if event is InputEventMouseButton:
		if event.button_index == BUTTON_LEFT:
			deselect_all()

func _on_copy_selected_button_up() -> void:
	var tags:String = ""
	var delim:String = delimiter
	for tag in selected_tags: tags += tag + delim
	OS.set_clipboard(tags)

func _on_select_all_button_up() -> void:
	for tag in current_tags: 
		select(tag)

func _on_paste_selected_button_up() -> void:
	var tags:String = OS.get_clipboard()
	if tags == "": return
	tag_entered(tags)

func _on_invert_selection_button_up() -> void:
	for tag in tags_array:
		if selected_tags.has(tag): deselect(tag)
		else: select(tag)
