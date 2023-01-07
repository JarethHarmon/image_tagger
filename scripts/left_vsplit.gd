extends VSplitContainer

func _ready() -> void: 
	Signals.connect("settings_loaded", self, "update_offset")
	
func update_offset() -> void: 
	self.split_offset = Global.GetOffsetThumbnailsV()

func _on_vsplit_dragged(offset:int) -> void: 
	Global.SetOffsetThumbnailsV(offset)
