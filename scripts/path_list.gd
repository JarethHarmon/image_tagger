extends PanelContainer

onready var hsplit1:HSplitContainer = $margin/vbox/hsplit1
onready var hsplit11:HSplitContainer = $margin/vbox/hsplit1/hsplit11
onready var hsplit12:HSplitContainer = $margin/vbox/hsplit1/hsplit12

onready var hsplit2:HSplitContainer = $margin/vbox/hsplit2
onready var hsplit21:HSplitContainer = $margin/vbox/hsplit2/hsplit21
onready var hsplit22:HSplitContainer = $margin/vbox/hsplit2/hsplit22

onready var indices:ItemList = $margin/vbox/hsplit2/hsplit21/indices
onready var paths:ItemList = $margin/vbox/hsplit2/hsplit21/paths
onready var types:ItemList = $margin/vbox/hsplit2/hsplit22/types
onready var sizes:ItemList = $margin/vbox/hsplit2/hsplit22/sizes

func _ready() -> void:
	var vscrollbars:Array = [indices.get_v_scroll(), paths.get_v_scroll(), types.get_v_scroll(), sizes.get_v_scroll()]
	for vscroll in vscrollbars:
		var sbe:StyleBoxEmpty = StyleBoxEmpty.new()
		#vscroll.add_stylebox_override("grabber", sbe)
		#vscroll.add_stylebox_override("scroll", sbe)

func _on_hsplit2_dragged(offset:int) -> void: hsplit1.split_offset = offset
func _on_hsplit21_dragged(offset:int) -> void: hsplit11.split_offset = offset
func _on_hsplit22_dragged(offset:int) -> void: hsplit12.split_offset = offset

func none_have_focus() -> bool:
	if indices.has_focus(): return false
	if paths.has_focus(): return false
	if types.has_focus(): return false
	if sizes.has_focus(): return false
	return true

# now the issue is the scroll bars; they are not synced
# options are try to ignore their input, or try to sync them
# either way will have to connect to the signals in code
var scroll_value:int = 0
func _on_indices_gui_input(event:InputEvent) -> void:
	if indices.has_focus() or none_have_focus():
		scroll_value = indices.get_v_scroll().value
		_on_paths_gui_input(event, true)
	
func _on_paths_gui_input(event:InputEvent, override:bool=false) -> void:
	if paths.has_focus() or none_have_focus():
		if not override:
			scroll_value = paths.get_v_scroll().value
	indices.get_v_scroll().value = scroll_value
	paths.get_v_scroll().value = scroll_value
	types.get_v_scroll().value = scroll_value
	sizes.get_v_scroll().value = scroll_value
	
func _on_types_gui_input(event:InputEvent) -> void:
	if types.has_focus() or none_have_focus():
		scroll_value = types.get_v_scroll().value
		_on_paths_gui_input(event, true)

func _on_sizes_gui_input(event:InputEvent) -> void:
	if sizes.has_focus() or none_have_focus():
		scroll_value = sizes.get_v_scroll().value
		_on_paths_gui_input(event, true)

# only the path selection should call other functions; any other selection should just change the selected items (including path selection, which will then call other functions)
func _on_indices_multi_selected(index:int, selected:bool) -> void:
	paths.unselect_all()
	types.unselect_all()
	sizes.unselect_all()
	for idx in indices.get_selected_items():
		paths.select(idx, false)
		types.select(idx, false)
		sizes.select(idx, false)

func _on_paths_multi_selected(index:int, selected:bool) -> void:
	# call other relevant functions (deleting paths from list for example)
	indices.unselect_all()
	types.unselect_all()
	sizes.unselect_all()
	for idx in paths.get_selected_items():
		indices.select(idx, false)
		types.select(idx, false)
		sizes.select(idx, false)

func _on_types_multi_selected(index:int, selected:bool) -> void:
	indices.unselect_all()
	paths.unselect_all()
	sizes.unselect_all()
	for idx in types.get_selected_items():
		paths.select(idx, false)
		indices.select(idx, false)
		sizes.select(idx, false)

func _on_sizes_multi_selected(index:int, selected:bool) -> void:
	indices.unselect_all()
	paths.unselect_all()
	types.unselect_all()
	for idx in sizes.get_selected_items():
		paths.select(idx, false)
		types.select(idx, false)
		indices.select(idx, false)
