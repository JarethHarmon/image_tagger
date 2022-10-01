extends Control

onready var organization_advanced_popup:Control = $organization_advanced_popup # this will likely be under the popups section in main/ instead
onready var search_bar:LineEdit = $panel/margin/vbox/hbox1/search_bar

var current_text:String = ""

func _on_search_bar_focus_entered() -> void: search_bar.text = "  " + search_bar.text.lstrip(" ")
func _on_search_bar_focus_exited() -> void: if search_bar.text.replace(" ", "") == "": search_bar.text = ""

func _on_search_button_button_up() -> void: print(current_text)
func _on_search_bar_text_entered(new_text:String) -> void: _on_search_button_button_up()
func _on_search_bar_text_changed(new_text:String) -> void: 
	if search_bar.text == "":
		current_text = ""
		search_bar.release_focus()
	else: current_text = new_text.lstrip(" ")		

func _on_advanced_search_show_button_up() -> void: organization_advanced_popup.show() # actual will call popup()
# also popup() will close itself automatically when user clicks out of bounds (so no need to implement that here)
 
