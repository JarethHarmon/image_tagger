extends Node

signal settings_loaded

signal resize_preview_image

signal change_page
signal page_changed(current_page)
signal max_pages_changed(count)
signal search_pressed(tags_all, tags_any, tags_none, new_query)
signal image_import_finished(tab_id)

signal sort_changed
signal order_changed
signal similarity_changed

signal load_full_image(image_hash, path)
signal load_image_tags(image_hash, selected_items)
signal create_path_buttons(image_hash, paths)
signal create_import_buttons(image_hash, imports)

signal update_import_button(tab_id, finished, success_count, total_count, import_name)
signal increment_all_button # increments its text by 1
signal increment_import_buttons(tab_ids) # increments their text by 1
signal finish_import_buttons(tab_ids) # sets their text to successCount

signal toggle_thumbnail_tooltips

signal select_all_pressed
signal all_selected_items(selection)
signal deselect_all_pressed

signal start_scan(folder, recursive)
signal files_selected(files)
signal folder_selected(folder)
signal new_import_started(import_id, count, import_name) # something will need to start the imageimporter (it will need the import_id, count, and access to imagescanner)
signal new_import_canceled

signal create_similarity_tab(image_hash)

signal switch_sort_buttons(swap) # bool

signal show_import_menu

signal toggle_file_section(visible)
signal toggle_tab_section(visible)
signal toggle_search_section(visible)
signal toggle_thumbnail_section(visible)
signal toggle_preview_section(visible)
signal toggle_tablist_section(visible)
signal toggle_tag_section(visible)

signal image_count_changed(total)

signal update_default_camera_position(new_position)

# core_buttons
signal file_button_pressed(position)
signal edit_button_pressed(position)
signal view_button_pressed(position)
signal help_button_pressed(position)

# nodes_readied
signal page_label_ready(node_path)
signal import_info_load_finished

# button_pressed
signal add_files
signal add_folders
signal tab_button_pressed(tab_id)
signal clear_pressed

signal set_rating(rating_name, rating_value)
signal rating_set(rating_name, rating_value)

# animation
signal set_animation_info(total_frames, fps)
signal add_animation_texture(texture, path, delay, new_image)
signal finish_animation(path)
