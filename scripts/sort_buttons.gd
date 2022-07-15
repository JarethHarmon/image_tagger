extends HFlowContainer

onready var sortby_options:OptionButton = $sort_by
onready var orderby_options:OptionButton = $order_by
onready var select_all:Button = $select_all

func _ready() -> void: Signals.connect("settings_loaded", self, "_apply_settings")
func _apply_settings() -> void: 
	sortby_options.selected = Globals.settings.current_sort
	orderby_options.selected = Globals.settings.current_order

func _on_sort_by_item_selected(index:int) -> void:
	Globals.settings.current_sort = index
	Signals.emit_signal("sort_changed")
	
func _on_order_by_item_selected(index:int) -> void:
	Globals.settings.current_order = index
	Signals.emit_signal("order_changed")

func _on_select_all_button_up() -> void:
	Signals.emit_signal("select_all_pressed")
