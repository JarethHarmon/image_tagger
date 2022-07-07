extends HFlowContainer

onready var prev_button:Button = $prev_page
onready var next_button:Button = $next_page
onready var page_label:Label = $vbox/page_label
onready var page_buttons:HBoxContainer = $vbox/hbox

var current_page:int = 1
var max_pages:int = 1

func _ready() -> void: 
	Signals.connect("max_pages_changed", self, "_max_pages_changed")
	change_page(1)
	
# call with relevant page when refreshing or changing to a different import tab (ie load relevant page even if same one)
func change_page(page:int) -> void: 
	if (page < 1): return
	if (page > max_pages): return
	current_page = page
	Signals.emit_signal("page_changed", page)
	page_label.text = String(current_page) + " / " + String(max_pages)
	
func _max_pages_changed(page_count:int) -> void: max_pages = page_count
func _on_prev_page_pressed() -> void: 
	prev_button.release_focus()
	change_page(current_page-1)
func _on_next_page_pressed() -> void: 
	next_button.release_focus()
	change_page(current_page+1)

