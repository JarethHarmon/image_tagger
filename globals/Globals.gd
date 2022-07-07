extends Node

var settings:Dictionary = {
  # Shaders
	"use_smooth_pixel" : true,
	"use_filter" : true,
	"use_color_grading" : true,
	"use_fxaa" : true,
	"use_edge_mix" : true,
}

func get_komi_hash(path:String) -> String: 
	var gob:Gob = Gob.new()
	var komi:String = gob.get_komi_hash(path)
	gob.queue_free()
	return komi

func get_sha512(path:String) -> String:
	var gob:Gob = Gob.new()
	var sha512:String = gob.get_sha512_hash(path)
	gob.queue_free()
	return sha512

func get_sha256(path:String) -> String: return File.new().get_sha256(path)

