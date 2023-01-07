extends HSplitContainer

onready var left:VBoxContainer = $left
onready var right:HSplitContainer = $right
var old_offset:int = 0

func _on_hsplit_dragged(offset:int) -> void:
	right.split_offset += (old_offset-offset)/2
	old_offset = offset
	Global.SetOffsetMainH(offset)

func _ready() -> void: 
	Signals.connect("settings_loaded", self, "update_offset")
	
func update_offset() -> void: 
	self.split_offset = Global.GetOffsetMainH()
