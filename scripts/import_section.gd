extends MarginContainer

onready var import_list:HFlowContainer = $panel/margin/flow

func _ready() -> void:
	Signals.connect("load_metadata", self, "create_import_buttons")

func create_import_buttons(image_hash:String) -> void:
	if MetadataManager.IncorrectImage(image_hash): return
	
	for child in import_list.get_children():
		child.queue_free()
	
	var imports:Array = MetadataManager.GetCurrentImports()	
	for import in imports:
		var b:Button = Button.new()
		b.text = "  " + import + "  "
		b.connect("pressed", self, "button_pressed", [import])
		var color:Color = Global.GetRandomGodotPastelColor()
		var sbf:StyleBoxFlat = Globals.make_stylebox(color, 1.0)
		b.add_stylebox_override("normal", sbf)
		b.add_color_override("font_color", Color.black)
		import_list.add_child(b)

func button_pressed(import_id:String) -> void:
	if import_id != "":
		OS.set_clipboard(import_id)
