extends HSplitContainer

onready var left:VSplitContainer = $vsplit/left
onready var right:HSplitContainer = $right
var old_offset:int = 0

func _on_hsplit_dragged(offset:int) -> void:
	right.split_offset += (old_offset-offset)/2
	old_offset = offset
