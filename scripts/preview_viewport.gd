extends Control

onready var preview:TextureRect = $single_image/image_0
onready var shaders:Control = $single_image/Shaders

var ready:bool = false
func _ready() -> void: ready = true

func _on_Shaders_resized() -> void: 
	return
	if not ready: return
	var smaller:float = shaders.rect_size.x if shaders.rect_size.x > shaders.rect_size.y else shaders.rect_size.y
	var size:Vector2 = Vector2(smaller, smaller)
	var position:Vector2 = (self.rect_size-size)/2
	
	shaders.rect_size = size
	#shaders.rect_position = position
	preview.rect_size = size
	#preview.rect_position = position
	
	print(size, " : ", position)
