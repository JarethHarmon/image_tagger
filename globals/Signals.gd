extends Node

signal settings_loaded

signal resize_preview_image

signal page_changed(current_page)
signal max_pages_changed(count)
signal search_pressed(tags_all, tags_any, tags_none, new_query)
signal image_import_finished(import_id)
signal sort_changed
signal order_changed

# core_buttons
signal file_button_pressed(position)
signal edit_button_pressed(position)
signal view_button_pressed(position)
signal help_button_pressed(position)

# nodes_readied
signal page_label_ready(node_path)
signal import_info_load_finished

# button_pressed
signal group_button_pressed(import_id)
signal clear_pressed
