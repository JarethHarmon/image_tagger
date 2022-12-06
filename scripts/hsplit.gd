extends HSplitContainer

onready var left:VBoxContainer = $left
onready var right:HSplitContainer = $right
var old_offset:int = 0

func _on_hsplit_dragged(offset:int) -> void:
	right.split_offset += (old_offset-offset)/2
	old_offset = offset
	Globals.settings.main_horizontal_offset = offset

func _ready() -> void: Signals.connect("update_main_horizontal_offset", self, "update_offset")
func update_offset(value:int) -> void: self.split_offset = value
