extends Control

onready var button_list:HBoxContainer = $margin/hbox/scroll/button_list

var buttons:Dictionary = {}

func _ready() -> void:
	# need to have import get an import_id from ImageImporter 
	# then pass that and the count along with the signal
	# then create the button here
	Signals.connect("new_import_started", self, "create_group_button")

func _on_all_button_button_up() -> void: Signals.emit_signal("group_button_pressed", "All")
func _on_group_button_pressed(import_id:String) -> void: Signals.emit_signal("group_button_pressed", import_id)

func create_group_button(import_id:String, count:int) -> void:
	var b:Button = Button.new()
	b.text = "  Import (0, 0/" + String(count) + ")  " 
	b.connect("button_up", self, "_on_group_button_pressed", [import_id])
	button_list.add_child(b)
	buttons[import_id] = b

func update_button_text(import_id:String, success_count:int, total_count:int, import_name="Import") -> void:
	buttons[import_id].text = "  %s (%d, %d/%d)  " % [import_name, success_count, success_count, total_count]

