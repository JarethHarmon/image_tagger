extends HFlowContainer

onready var sortby_options:OptionButton = $sort_by
onready var orderby_options:OptionButton = $order_by
onready var similarity:OptionButton = $similarity
onready var select_all:Button = $select_all

var deselect:bool = false

func _input(event:InputEvent) -> void:
	if Input.is_action_pressed("ctrl"): 
		deselect = true
		select_all.text = "  Deselect All  "
	if Input.is_action_just_released("ctrl"): 
		deselect = false
		select_all.text = "  Select All  "
	
func _ready() -> void: 
	Signals.connect("settings_loaded", self, "_apply_settings")
	Signals.connect("switch_sort_buttons", self, "_switch_sort_buttons")
	Signals.connect("settings_loaded", self, "apply_settings")

func apply_settings() -> void:
	$thumbnail_size.value = Global.GetThumbnailWidth()
	$thumbnail_size_entry.value = Global.GetThumbnailWidth()

func _switch_sort_buttons(swap:bool) -> void:
	sortby_options.visible = not swap
	orderby_options.visible = not swap
	similarity.visible = swap
	
func _apply_settings() -> void: 
	sortby_options.selected = Global.GetCurrentSort()
	orderby_options.selected = Global.GetCurrentOrder()

func _on_sort_by_item_selected(index:int) -> void:
	Global.SetCurrentSort(index)
	Signals.emit_signal("sort_changed")
	
func _on_order_by_item_selected(index:int) -> void:
	Global.SetCurrentOrder(index)
	Signals.emit_signal("order_changed")

func _on_select_all_button_up() -> void:
	if deselect: Signals.emit_signal("deselect_all_pressed")
	else: Signals.emit_signal("select_all_pressed")

func _on_similarity_item_selected(index:int) -> void:
	Global.SetCurrentSortSimilarity(index)
	Signals.emit_signal("similarity_changed")
