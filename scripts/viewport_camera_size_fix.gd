extends ViewportContainer

func _on_ViewportContainer_resized():
	Signals.emit_signal("update_default_camera_position", self.rect_size/2)

