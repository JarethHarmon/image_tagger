extends VSplitContainer

func _ready() -> void: Signals.connect("update_thumbnails_vertical_offset", self, "update_offset")
func update_offset(value:int) -> void: self.split_offset = value


func _on_vsplit_dragged(offset:int) -> void: Globals.settings.thumbnails_vertical_offset = offset
