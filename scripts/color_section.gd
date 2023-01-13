extends HBoxContainer

onready var options:OptionButton = $select_color
onready var colors:HFlowContainer = $scroll/colors

var curr_color:int
var num_colors:int = 0
var max_colors:int = 8

func _ready() -> void:
	Signals.connect("settings_loaded", self, "_settings_loaded")
	Signals.connect("switch_sort_buttons", self, "_switch_sort_buttons")

func _on_select_color_item_selected(index:int) -> void: curr_color = index

func _on_confirm_color_pressed() -> void:
	if num_colors == max_colors: return
	num_colors += 1
	
	var color:Color = Color.red
	var font:Color = Color.white
	if curr_color == Globals.Colors.GREEN: color = Color.green
	elif curr_color == Globals.Colors.BLUE: color = Color.blue
	elif curr_color == Globals.Colors.YELLOW: color = Color.yellow
	elif curr_color == Globals.Colors.CYAN: color = Color.cyan
	elif curr_color == Globals.Colors.FUCHSIA: color = Color.fuchsia
	elif curr_color == Globals.Colors.LIGHT: 
		color = Color.white
		font = Color.black
	elif curr_color == Globals.Colors.DARK: color = Color.black
	elif curr_color == Globals.Colors.ALPHA: color = Color.transparent
	
	var sbf:StyleBoxFlat = Globals.make_stylebox(color, 0.6) 
	var button:Button = Button.new()
	var text:String = options.get_item_text(curr_color)
	button.text = "  " + text + "  "
	button.add_stylebox_override("normal", sbf)
	button.set("custom_colors/font_color", font)
	button.connect("pressed", self, "button_pressed", [curr_color, button])
	colors.add_child(button)
	ThumbnailManager.AddColor(curr_color)

func button_pressed(color:int, button:Button) -> void:
	ThumbnailManager.RemoveColor(color)
	button.queue_free()
	num_colors -= 1

func _on_clear_pressed() -> void:
	ThumbnailManager.ClearColors()
	for child in colors.get_children():
		child.queue_free()
	num_colors = 0

func _settings_loaded() -> void:
	call_deferred("settings_loaded")

func settings_loaded() -> void:
	_on_sort_by_item_selected(Global.GetCurrentSort())

func _switch_sort_buttons(hide:bool) -> void:
	self.visible = not hide

func _on_sort_by_item_selected(index:int) -> void:
	if index == Globals.Sort.COLOR: self.visible = true
	else: self.visible = false
