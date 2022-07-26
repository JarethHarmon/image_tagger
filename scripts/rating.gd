extends HBoxContainer

const star:Texture = preload("res://assets/star.png")
const star_half:Texture = preload("res://assets/star-half.png")
const star_empty:Texture = preload("res://assets/star-empty.png")
const star_fade:Texture = preload("res://assets/star-fade.png")
const star_half_fade:Texture = preload("res://assets/star-half-fade.png")

var rating:int = 0

func _ready() -> void:
	Signals.connect("set_rating", self, "_set_rating")

func _set_rating(value:int) -> void:
	rating = value
	var children:Array = self.get_children()
	for child in children: child.texture = star_empty
	for i in rating/2: children[i].texture = star
	if rating % 2 == 1: children[(rating/2)].texture = star_half

func _on_rating_gui_input(event:InputEvent) -> void:
	if event is InputEventMouseMotion:
		var percent:float = event.position.x / self.rect_size.x
		var offset:int = (percent * 10) + 1
		var children:Array = self.get_children()
		if offset == rating:
			for i in offset/2: children[i].texture = star_fade
			if offset % 2 == 1: children[(offset/2)].texture = star_half_fade
			for i in rating/2: children[i].texture = star
			if rating % 2 == 1: children[(rating/2)].texture = star_half
		elif offset > rating:
			for i in offset/2: children[i].texture = star_fade
			if offset % 2 == 1: children[(offset/2)].texture = star_half_fade
			for i in rating/2: children[i].texture = star
		else:
			for i in range(offset/2, children.size()): children[i].texture = star_empty
			if offset % 2 == 1: children[offset/2].texture = star_half_fade
			else: children[(offset/2)-1].texture = star_fade
		
	elif event is InputEventMouseButton:
		var percent:float = event.position.x / self.rect_size.x
		var offset:int = (percent * 10) + 1
		var children:Array = self.get_children()
		for i in offset/2: children[i].texture = star
		if offset % 2 == 1: children[(offset/2)].texture = star_half
		rating = offset
		Signals.emit_signal("rating_set", rating)
				
func _on_rating_mouse_exited() -> void:
	var children:Array = self.get_children()
	for child in children:
		if child.texture == star_fade or child.texture == star_half_fade:
			child.texture = star_empty
	for i in rating/2: children[i].texture = star
	if rating % 2 == 1: children[(rating/2)].texture = star_half

