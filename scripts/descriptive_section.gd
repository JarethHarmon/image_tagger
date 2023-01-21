extends MarginContainer

# I might merge the individual tag sections into a single tag_section.gd script

onready var delimiter_entry:TagEdit = $vbox/hbox/delimiter
onready var tag_entry:TagEdit = $vbox/hbox/tag_entry
onready var tag_flow:HFlowContainer = $vbox/margin/hbox/scroll/panel/flow
onready var scroll:ScrollContainer = $vbox/margin/hbox/scroll

var selected_tags:Dictionary = {}
var selected_thumbnails:Dictionary = {}
var current_tags:Dictionary = {}
var tags_array:Array = []
var delimiter:String = ","
var last_clicked_index:int = -1

func _unhandled_input(event:InputEvent) -> void:
	if event is InputEventKey:
		if event.scancode == KEY_V:
			_on_paste_pressed()
		elif event.scancode == KEY_T:
			# also need to set scroll to 0 and ensure tag section is visible
			# note that scroll here refers to metadata/margin/scroll
			Signals.emit_signal("make_descriptive_tags_visible")
			yield(get_tree(), "idle_frame")
			tag_entry.grab_focus()
		elif event.scancode == KEY_C:
			_on_copy_pressed()
		elif event.scancode == KEY_A:
			_on_select_pressed()
		elif event.scancode == KEY_I:
			_on_invert_pressed()
		elif event.scancode == KEY_DELETE:
			_on_remove_pressed()

func _ready() -> void:
	Signals.connect("all_selected_items", self, "select_all")
	Signals.connect("tab_button_pressed", self, "clear_selection")
	Signals.connect("load_image_tags", self, "download_tags")

func _on_delimiter_text_changed(text:String) -> void:
	delimiter = text

func _on_tag_entry_text_entered(text:String) -> void:
	if text == "": return
	var use_delimiter:bool = false if delimiter == "" else Global.GetUseDelimiter()
	var tags:Array = text.split(delimiter, false) as Array if use_delimiter else [text]
	for tag in tags:
		add_tag(tag)
	tag_entry.text = ""
	scroll_to_end()
	upload_tags(tags, selected_thumbnails)

func scroll_to_end() -> void:
	var scrollbar:VScrollBar = scroll.get_v_scrollbar()
	yield(get_tree(), "idle_frame")
	yield(get_tree(), "idle_frame")
	scrollbar.value = scrollbar.max_value

func add_tag(tag:String) -> void:
	if current_tags.has(tag): return
	current_tags[tag] = null
	
	var b:Button = Button.new()
	b.text = "  " + tag.strip_edges() + "  "
	var color:Color = Globals.make_color()
	if Global.GetUseColoredText():
		if Global.GetUseSeparateFontColor():
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
	b.connect("pressed", self, "tag_clicked", [tag])

	current_tags[tag] = {
		"color" : color,
		"normal" : sbf,
		"focus" : sbf2,
		"button" : b
	}
	tag_flow.add_child(b)
	tags_array.push_back(tag)

# note: should look into doing the threading on csharp side instead
func upload_tags(tags:Array, selection:Dictionary) -> void:
	var selected_hashes:Array = []
	for idx in selection: selected_hashes.push_back(selection[idx])
	if selected_hashes.empty(): return
	var thread:Thread = Thread.new()
	thread.start(self, "_thread", [thread, tags, selected_hashes])

func download_tags(image_hash:String, selection:Dictionary) -> void:
	if MetadataManager.IncorrectImage(image_hash): return
	clear_tag_list()
	var tags:Array = MetadataManager.GetCurrentTags()
	for tag in tags: add_tag(tag)
	selected_thumbnails = selection	

# note: I need to figure out where in this process the actual TagInfo objects should be created
func _thread(args:Array) -> void:
	var t:Thread = args[0]
	var tags:Array = args[1]
	var hashes:Array = args[2]
	MetadataManager.AddTags(hashes, tags)
	t.call_deferred("wait_to_finish")
	
func tag_clicked(tag:String) -> void:
	var button:Button = current_tags[tag].button
	var index:int = button.get_index()
	
	if Input.is_action_pressed("ctrl") and Input.is_action_pressed("shift"): pass
	elif Input.is_action_pressed("ctrl"):
		invert_tag_selection(tag)
	elif Input.is_action_pressed("shift"):
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
					if i == last_clicked_index: deselect(curr_tag)
					if i == index: select(curr_tag)
	else:
		deselect_all()
		selected_tags[tag] = button
		current_tags[tag].button.add_stylebox_override("normal", current_tags[tag].focus)
		current_tags[tag].button.add_color_override("font_color", Color.black)
	
	last_clicked_index = index
	button.release_focus()

func select(tag:String) -> void:
	current_tags[tag].button.add_stylebox_override("normal", current_tags[tag].focus)
	current_tags[tag].button.add_color_override("font_color", Color.black)
	selected_tags[tag] = null

func select_all(selection:Dictionary) -> void:
	selected_thumbnails = selection

func deselect(tag:String) -> void:
	current_tags[tag].button.add_stylebox_override("normal", current_tags[tag].normal)
	if Global.GetUseColoredText():
		current_tags[tag].button.add_color_override("font_color", current_tags[tag].color * 1.5)
	else:
		current_tags[tag].button.remove_color_override("font_color")
	selected_tags.erase(tag)

func deselect_all() -> void:
	for tag in selected_tags: 
		current_tags[tag].button.add_stylebox_override("normal", current_tags[tag].normal)
		if Global.GetUseColoredText():
			current_tags[tag].button.add_color_override("font_color", current_tags[tag].color * 1.5)
		else:
			current_tags[tag].button.remove_color_override("font_color")
	selected_tags.clear()

func invert_tag_selection(tag:String) -> void:
	if selected_tags.has(tag): deselect(tag)
	else: select(tag)

func clear_selection(_tab_id:String="") -> void:
	selected_thumbnails.clear()
	clear_tag_list()

func clear_tag_list() -> void:
	current_tags.clear()
	tags_array.clear()
	selected_tags.clear()
	for child in tag_flow.get_children():
		child.queue_free()

func _on_select_pressed():
	for tag in current_tags:
		select(tag)

func _on_copy_pressed():
	var tags:String = ""
	var delim:String = delimiter
	for tag in selected_tags: tags += tag + delim
	if tags != "": OS.set_clipboard(tags)

func _on_paste_pressed():
	var tags:String = OS.get_clipboard()
	if tags == "": return
	_on_tag_entry_text_entered(tags)

func _on_invert_pressed():
	for tag in tags_array:
		invert_tag_selection(tag)

func _on_remove_pressed():
	var tags:Array = selected_tags.keys()
	for tag in tags:
		selected_tags.erase(tag)
		tags_array.erase(tag)
		current_tags[tag].button.queue_free()
		current_tags.erase(tag)
	var hashes:Array = []
	for idx in selected_thumbnails:
		hashes.push_back(selected_thumbnails[idx])
	MetadataManager.RemoveTags(hashes, tags)

func _on_remove_all_pressed():
	var tags:Array = current_tags.keys()
	for tag in tags:
		selected_tags.erase(tag)
		tags_array.erase(tag)
		current_tags[tag].button.queue_free()
	current_tags.clear()
	var hashes:Array = []
	for idx in selected_thumbnails:
		hashes.push_back(selected_thumbnails[idx])
	MetadataManager.RemoveTags(hashes, tags)

