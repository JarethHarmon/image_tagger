extends Control

func _ready() -> void:
	for i in 20:
		call_deferred("test")

func test() -> void:
	yield(get_tree().create_timer(1), "timeout")
	var t:Thread = Thread.new()
	t.start(self, "_test", t)

func _test(t:Thread) -> void:
	for i in 1000000: var a = i
	call_deferred("close", t)

func close(t:Thread) -> void:
	if t.is_active() or t.is_alive():
		t.wait_to_finish()
	test()

