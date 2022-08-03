extends HBoxContainer

export (Texture) var icon
export (Texture) var icon_half
export (Texture) var icon_empty
export (Texture) var icon_fade
export (Texture) var icon_half_fade

export (String) var rating_name
var rating:int = 0

func _ready() -> void:
	Signals.connect("set_rating", self, "_set_rating")

func _set_rating(_name:String, _value:int) -> void:
	if rating_name != _name: return
	rating = _value
	var children:Array = self.get_children()
	for child in children: child.texture = icon_empty
	for i in rating/2: children[i].texture = icon
	if rating % 2 == 1: children[(rating/2)].texture = icon_half

func _on_rating_gui_input(event:InputEvent) -> void:
	if event is InputEventMouseMotion:
		var percent:float = event.position.x / self.rect_size.x
		var offset:int = (percent * 10) + 1
		var children:Array = self.get_children()
		if offset == rating:
			for i in offset/2: children[i].texture = icon_fade
			if offset % 2 == 1: children[(offset/2)].texture = icon_half_fade
			for i in rating/2: children[i].texture = icon
			if rating % 2 == 1: children[(rating/2)].texture = icon_half
		elif offset > rating:
			for i in offset/2: children[i].texture = icon_fade
			if offset % 2 == 1: children[(offset/2)].texture = icon_half_fade
			for i in rating/2: children[i].texture = icon
		else:
			for i in range(offset/2, children.size()): children[i].texture = icon_empty
			if offset % 2 == 1: children[offset/2].texture = icon_half_fade
			else: children[(offset/2)-1].texture = icon_fade
		
	elif event is InputEventMouseButton:
		if event.pressed:
			var percent:float = event.position.x / self.rect_size.x
			var offset:int = (percent * 10) + 1
			var children:Array = self.get_children()
			for i in offset/2: children[i].texture = icon
			if offset % 2 == 1: children[(offset/2)].texture = icon_half
			rating = offset
			Signals.emit_signal("rating_set", rating_name, rating)
				
func _on_rating_mouse_exited() -> void:
	var children:Array = self.get_children()
	for child in children:
		if child.texture == icon_fade or child.texture == icon_half_fade:
			child.texture = icon_empty
	for i in rating/2: children[i].texture = icon
	if rating % 2 == 1: children[(rating/2)].texture = icon_half

