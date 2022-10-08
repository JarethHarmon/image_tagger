extends LineEdit
class_name TagEdit

func _gui_input(event:InputEvent) -> void:
	if event is InputEventKey:
		if event.scancode == KEY_LEFT or event.scancode == KEY_RIGHT:
			if self.text == "":
				self.release_focus()
