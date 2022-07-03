extends MarginContainer

func _input(event:InputEvent) -> void:
	if Input.is_action_just_pressed("hide_ui"):
		_on_visibility_pressed()

func _on_visibility_pressed() -> void: self.visible = not self.visible
