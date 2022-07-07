extends PanelContainer

onready var hsplit1:HSplitContainer = $margin/vbox/hsplit1
onready var hsplit11:HSplitContainer = $margin/vbox/hsplit1/hsplit11
onready var hsplit12:HSplitContainer = $margin/vbox/hsplit1/hsplit12

onready var hsplit2:HSplitContainer = $margin/vbox/hsplit2
onready var hsplit21:HSplitContainer = $margin/vbox/hsplit2/hsplit21
onready var hsplit22:HSplitContainer = $margin/vbox/hsplit2/hsplit22

func _on_hsplit2_dragged(offset:int) -> void: hsplit1.split_offset = offset
func _on_hsplit21_dragged(offset:int) -> void: hsplit11.split_offset = offset
func _on_hsplit22_dragged(offset:int) -> void: hsplit12.split_offset = offset
