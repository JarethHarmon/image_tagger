using Godot;
using System;

public class organization : Control
{
	private Node signals, globals;
	
	public override void _Ready()
	{
		signals = GetNode<Node>("/root/Signals");
		globals = GetNode<Node>("/root/Globals");
		
		signals.Call("connect", "toggle_search_section", this, "ToggleSearchSection");
	}
	
	private void ToggleSearchSection(bool visible)
	{
		this.Visible = visible;
		globals.Call("toggle_parent_visibility_from_children", this);
		globals.Call("toggle_parent_visibility_from_children", this.GetParent());
	}
	
	private void _on_clear_all_pressed()
	{
		signals.Call("emit_signal", "clear_pressed");
	}
	private void _on_default_pressed()
	{
		signals.Call("emit_signal", "default_pressed");
	}
}



