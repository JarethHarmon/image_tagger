extends Control

onready var file_button:Button = $margin/flow/file_button
onready var view_button:Button = $margin/flow/view_button

func _on_file_button_pressed() -> void: 
	var position:Vector2 = Vector2(file_button.rect_position.x + 20, file_button.rect_position.y + file_button.rect_size.y + 10)
	Signals.emit_signal("file_button_pressed", position)
	
func _on_view_button_button_up() -> void:
	var position:Vector2 = Vector2(view_button.rect_position.x + 20, view_button.rect_position.y + view_button.rect_size.y + 10)
	Signals.emit_signal("view_button_pressed", position)

func _ready() -> void:
	Signals.connect("toggle_file_section", self, "_toggle_file_section")

func _toggle_file_section(_visible:bool) -> void: self.visible = _visible
