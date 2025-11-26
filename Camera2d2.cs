using Godot;
using System;

public partial class Camera2d2 : Camera2D
{
	[Export]
	public NodePath TargetPath;

	private Node2D target;

	public override void _Ready()
	{
		target = GetNode<Node2D>(TargetPath);
	}

	public override void _Process(double delta)
	{
		if (target == null) return;

		GlobalPosition = target.GlobalPosition;
	}
}
