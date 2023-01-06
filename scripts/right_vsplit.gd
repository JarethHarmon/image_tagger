extends VSplitContainer

func _ready() -> void: 
	Signals.connect("update_vertical_metadata_offset", self, "update_offset")
	
func update_offset(value:int) -> void: 	
	self.split_offset = value

func _on_vsplit_dragged(offset:int) -> void:
	Global.SetOffsetMetadataV(offset)
