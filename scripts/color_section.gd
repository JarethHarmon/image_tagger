extends HBoxContainer

onready var label:Label = $colors_temp
onready var options:OptionButton = $select_color

var curr_color:int
var colors:Array = []

func _ready() -> void:
	Signals.connect("settings_loaded", self, "_settings_loaded")

func _on_select_color_item_selected(index:int) -> void: curr_color = index

func _on_confirm_color_pressed() -> void:
	if colors.has(curr_color): return
	colors.append(curr_color)
	ThumbnailManager.AddColor(curr_color)
	label.text += options.get_item_text(curr_color) + ", "

func _on_clear_pressed() -> void:
	colors.clear()
	ThumbnailManager.ClearColors()
	label.text = ""

func _settings_loaded() -> void:
	call_deferred("settings_loaded")

func settings_loaded() -> void:
	_on_sort_by_item_selected(Global.GetCurrentSort())

func _on_sort_by_item_selected(index:int) -> void:
	if index == Globals.Sort.COLOR: self.visible = true
	else: self.visible = false
