extends HFlowContainer

onready var include_all:LineEdit = $vbox2/include_all
onready var include_any:LineEdit = $vbox2/include_any
onready var exclude_all:LineEdit = $vbox2/exclude_all
onready var search_button:Button = $vbox3/search

var tags_all:Array = []
var tags_any:Array = []
var tags_none:Array = []

func _ready() -> void:
	search_button.connect("button_up", self, "search_pressed")
	Signals.connect("change_page", self, "page_changed")
	Signals.connect("tab_button_pressed", self, "tab_button_pressed")
	Signals.connect("image_import_finished", self, "refresh_page")
	Signals.connect("clear_pressed", self, "clear_pressed")
	Signals.connect("sort_changed", self, "search_pressed")
	Signals.connect("order_changed", self, "search_pressed")
	Signals.connect("similarity_changed", self, "search_pressed")

func _on_include_all_text_entered(new_text:String) -> void: search_pressed()
func _on_include_any_text_entered(new_text:String) -> void: search_pressed()
func _on_exclude_all_text_entered(new_text:String) -> void: search_pressed()

func _on_include_all_text_changed(new_text:String) -> void: tags_all = new_text.split(",", false)
func _on_include_any_text_changed(new_text:String) -> void: tags_any = new_text.split(",", false)
func _on_exclude_all_text_changed(new_text:String) -> void: tags_none = new_text.split(",", false)

func tab_button_pressed(tab_id:String) -> void:
	Globals.current_tab_id = tab_id
	Globals.current_tab_type = Database.GetTabType(tab_id)
	search_pressed()
func search_pressed() -> void: 
	Signals.emit_signal("search_pressed", tags_all, tags_any, tags_none, true)
func page_changed(new_page:int=1) -> void: Signals.emit_signal("search_pressed", tags_all, tags_any, tags_none, false)
func refresh_page(tab_id:String) -> void: 
	if tab_id == "": return
	if tab_id == Globals.current_tab_id: page_changed()
func clear_pressed() -> void: 
	tags_all.clear()
	tags_any.clear()
	tags_none.clear()
	include_all.text = ""
	include_any.text = ""
	exclude_all.text = ""
	search_pressed()
