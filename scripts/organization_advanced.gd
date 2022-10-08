extends Control

# I think I should replicate the tag entry section's functionality for each of these tag-filter-bars
#	> so each one has an entry area, and a view-area where the tags are individual buttons
#	> I would ideally like to be able to directly type in the view-area, but I am unsure how to implement that
#	> there might also be a larger tag viewing area below these bars where it shows all current conditions
#		>> this would include the conditions generated from all/any/none bars
#	> I need to change UI concept to allow for there to be multiple conditions for complex

#	I think I will change it to be an hscroll/hbox/[tag_buttons]::tag_entry
#	(order of [tag_buttons] and tag_entry might be switched
#	[tag_buttons] are just buttons, they would not be under another node (probably)
#	tag_entry would just be a lineedit set to expand
#	tag_entry should have a minumum size, and should initially take up the entire space
#	new tags will reduce the size of tag_entry until it hits its minumum size
#	after that the scroll container will allow more tags to be added and viewable


# might be better to make the entire vbox scrollable and just have each section take up as much space as it needs
#	in this case, remove rect_min_size.y from /tag_filters_section/vbox/scroll
#	might also need to adjust min size of tag_filters_section according to the size of $margin/vbox/tag_filters_section/vbox/scroll/vbox
