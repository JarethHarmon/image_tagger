extends HSplitContainer

func _ready() -> void: 
	Signals.connect("update_horizontal_metadata_offset", self, "update_offset")
	
func update_offset(value:int) -> void: 
	self.split_offset = value

func _on_hsplit_dragged(offset:int) -> void: 
	Global.SetOffsetMetadataH(offset)
