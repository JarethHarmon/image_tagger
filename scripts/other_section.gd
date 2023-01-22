extends MarginContainer

onready var sha256:Button = $vbox/hash/hbox/contents
onready var ahash:Button = $vbox/hbox1/ahash/hbox/contents
onready var dhash:Button = $vbox/hbox1/dhash/hbox/contents
onready var phash:Button = $vbox/hbox2/phash/hbox/contents
onready var whash:Button = $vbox/hbox2/whash/hbox/contents

onready var red:Label = $vbox/hbox3/red/hbox/contents
onready var green:Label = $vbox/hbox3/green/hbox/contents
onready var blue:Label = $vbox/hbox3/blue/hbox/contents
onready var yellow:Label = $vbox/hbox4/yellow/hbox/contents
onready var cyan:Label = $vbox/hbox4/cyan/hbox/contents
onready var fuchsia:Label = $vbox/hbox4/fuchsia/hbox/contents
onready var vivid:Label = $vbox/hbox5/vivid/hbox/contents
onready var neutral:Label = $vbox/hbox5/neutral/hbox/contents
onready var dull:Label = $vbox/hbox5/dull/hbox/contents
onready var light:Label = $vbox/hbox6/light/hbox/contents
onready var medium:Label = $vbox/hbox6/medium/hbox/contents
onready var dark:Label = $vbox/hbox6/dark/hbox/contents
onready var alpha:Label = $vbox/hbox6/alpha/hbox/contents

var phash_buttons:Array
var color_labels:Array

func _ready() -> void:
	Signals.connect("load_metadata", self, "load_metadata")
	phash_buttons = [ahash, dhash, phash, whash]
	for button in phash_buttons:
		button.connect("pressed", self, "button_pressed", [button])
	color_labels = [red, green, blue, yellow, cyan, fuchsia, vivid, neutral, dull, light, medium, dark, alpha]

func button_pressed(button:Button) -> void:
	OS.set_clipboard(button.text)

func load_metadata(image_hash:String) -> void:
	if MetadataManager.IncorrectImage(image_hash): return
	var phashes:Array = MetadataManager.GetCurrentPerceptualHashes()
	var colors:Array = MetadataManager.GetCurrentColors()
	
	sha256.text = image_hash
	for i in range(phashes.size()):
		phash_buttons[i].text = phashes[i]
		
	for i in range(colors.size()):
		color_labels[i].text = "%1.2f" % [float(colors[i] * 100) / 256] + "%"