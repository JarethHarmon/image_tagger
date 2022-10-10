extends Node

# was created in an attempt to fix an issue with lineedits, but it did not work and I fixed it another way
# it might still be a good idea to consolidate all input events in one script and communicate with signals though
# see "tag_list.gd"

# consider move signals to Signals.gd
signal paste_tags
signal copy_tags
signal grab_focus_tag_entry

#func _unhandled_input(event:InputEvent) -> void:
#	if event is InputEventKey:
#		if event.scancode == KEY_V: emit_signal("paste_tags")
#		elif event.scancode == KEY_T: emit_signal("grab_focus_tag_entry")
#		elif event.scancode == KEY_C: emit_signal("copy_tags")
#
