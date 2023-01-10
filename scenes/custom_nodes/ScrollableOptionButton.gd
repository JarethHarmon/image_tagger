extends OptionButton

func _on_ScrollableOptionButton_gui_input(event:InputEvent) -> void:
	if event is InputEventMouseButton:
		if event.button_index == BUTTON_WHEEL_DOWN and event.pressed:
			if selected < self.get_item_count() - 1:
				selected += 1
				emit_signal("item_selected", selected)
				
		elif event.button_index == BUTTON_WHEEL_UP and event.pressed:
			if selected > 0:
				selected -= 1
				emit_signal("item_selected", selected)
