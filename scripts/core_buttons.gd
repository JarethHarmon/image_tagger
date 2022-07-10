extends Control

onready var file_button:Button = $margin/flow/file_button

func _on_file_button_pressed() -> void: 
	var position:Vector2 = Vector2(file_button.rect_position.x + 20, file_button.rect_position.y + file_button.rect_size.y + 10)
	Signals.emit_signal("file_button_pressed", position)
	
