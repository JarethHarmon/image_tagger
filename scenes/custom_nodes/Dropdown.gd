extends VBoxContainer

const down_arrow:StreamTexture = preload("res://assets/dropdown.png")
const right_arrow:StreamTexture = preload("res://assets/dropdown-right.png")

onready var section:Control = get_child(1)
onready var dropdown_button:Button = $dropdown

var closed:bool = true # load state of dropdown from settings

func _ready() -> void:
	dropdown_button.connect("pressed", self, "dropdown_pressed")

func dropdown_pressed() -> void:
	section.visible = closed
	closed = not closed
	if closed: dropdown_button.icon = right_arrow
	else: dropdown_button.icon = down_arrow
