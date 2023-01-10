extends HFlowContainer

onready var include_all:LineEdit = $vbox2/include_all
onready var include_any:LineEdit = $vbox2/include_any
onready var exclude_all:LineEdit = $vbox2/exclude_all
onready var search_button:Button = $vbox3/search

onready var complex:LineEdit = $vbox2/complex
var tags_complex:Array = []
var tags_all:Array = []
var tags_any:Array = []
var tags_none:Array = []

func _ready() -> void:
	include_all.connect("text_entered", self, "_on_include_all_text_entered")
	include_all.connect("text_changed", self, "_on_include_all_text_changed")
	include_any.connect("text_entered", self, "_on_include_any_text_entered")
	include_any.connect("text_changed", self, "_on_include_any_text_changed")
	exclude_all.connect("text_entered", self, "_on_exclude_all_text_entered")
	exclude_all.connect("text_changed", self, "_on_exclude_all_text_changed")
	complex.connect("text_entered", self, "_on_complex_text_entered")
	complex.connect("text_changed", self, "_on_complex_text_changed")
	search_button.connect("button_up", self, "search_pressed")
	
	Signals.connect("change_page", self, "page_changed")
	Signals.connect("tab_button_pressed", self, "tab_button_pressed")
	Signals.connect("image_import_finished", self, "refresh_page")
	Signals.connect("clear_pressed", self, "clear_pressed")
	Signals.connect("default_pressed", self, "default_pressed")

func _on_include_all_text_entered(new_text:String) -> void: ThumbnailManager.QueryDatabaseGD(false)
func _on_include_any_text_entered(new_text:String) -> void: ThumbnailManager.QueryDatabaseGD(false)
func _on_exclude_all_text_entered(new_text:String) -> void: ThumbnailManager.QueryDatabaseGD(false)
func _on_complex_text_entered(new_text:String) -> void: ThumbnailManager.QueryDatabaseGD(false)

func _on_include_all_text_changed(new_text:String) -> void: 
	tags_all = new_text.split(",", false)
	ThumbnailManager.SetTagsAll(tags_all)
	
func _on_include_any_text_changed(new_text:String) -> void: 
	tags_any = new_text.split(",", false)
	ThumbnailManager.SetTagsAny(tags_any)

func _on_exclude_all_text_changed(new_text:String) -> void:
	tags_none = new_text.split(",", false)
	ThumbnailManager.SetTagsNone(tags_none)

func _on_complex_text_changed(new_text:String) -> void: 
	tags_complex = new_text.split(",", false)
	ThumbnailManager.SetTagsComplex(tags_all, tags_any, tags_none, tags_complex)

func tab_button_pressed(tab_id:String) -> void:
	Globals.current_tab_id = tab_id
	Global.SetCurrentTabId(tab_id)
	Globals.current_tab_type = MetadataManager.GetTabType(tab_id)
	ThumbnailManager.UpdateImportId(tab_id)
	if Globals.current_tab_type == Globals.TabType.SIMILARITY: Signals.emit_signal("switch_sort_buttons", true)
	else: Signals.emit_signal("switch_sort_buttons", false)

func search_pressed() -> void: ThumbnailManager.QueryDatabaseGD(true) # force update if search pressed, may change this to be F5 only
func page_changed(new_page:int=1) -> void: ThumbnailManager.UpdatePage(new_page)
func refresh_page(tab_id:String) -> void: 
	if tab_id == "": return
	if tab_id == Globals.current_tab_id: page_changed()

func default_pressed() -> void:
	ThumbnailManager.SetTagsComplex([], [], [], [])
	tags_all.clear()
	tags_any.clear()
	tags_none.clear()
	tags_complex.clear()
	include_all.text = ""
	include_any.text = ""
	exclude_all.text = ""
	complex.text = ""

func clear_pressed() -> void: 
	default_pressed()
	search_pressed()
