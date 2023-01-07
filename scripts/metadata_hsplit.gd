extends HSplitContainer

func _ready() -> void: 
	Signals.connect("settings_loaded", self, "update_offset")
	
func update_offset() -> void: 
	self.split_offset = Global.GetOffsetMetadataH()

func _on_hsplit_dragged(offset:int) -> void: 
	Global.SetOffsetMetadataH(offset)
