extends ScrollContainer

func _ready() -> void:
	var hsb:HScrollBar = self.get_h_scrollbar()
	var sbe:StyleBoxEmpty = StyleBoxEmpty.new()
	hsb.add_stylebox_override("grabber", sbe)
	hsb.add_stylebox_override("scroll", sbe)
