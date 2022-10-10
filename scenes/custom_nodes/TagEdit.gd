extends LineEdit
class_name TagEdit

func _gui_input(event:InputEvent) -> void:
	if event is InputEventKey:
		if event.scancode == KEY_LEFT or event.scancode == KEY_RIGHT:
			if self.text == "":
				self.release_focus()
		else: get_tree().set_input_as_handled() # prevents hotkeys typed into a lineedit from propagating to other nodes
		# note that the F_ keys (like F8) are in _input() so they will be called regardless of whether they have already been "handled"
