extends MarginContainer

onready var hsep:HSeparator = $vbox/hsep
onready var flow:HFlowContainer = $vbox/flow

var hidden:bool = false

func _input(event:InputEvent) -> void:
	if Input.is_action_just_pressed("hide_ui"):
		_on_visibility_pressed()

func _on_visibility_pressed() -> void: 
	hidden = not hidden
	if hidden:
		hsep.hide()
		flow.hide()
	else:
		hsep.show()
		flow.show()
