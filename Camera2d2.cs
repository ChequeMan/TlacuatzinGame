using Godot;
using System;

public partial class Camera2d2 : Camera2D
{
	[Export]
	public NodePath TargetPath;

	private Node2D target;
	
	private bool isZooming = false;//new
	private Vector2 normalZoom = new Vector2(1f, 1f);//new

	public override void _Ready()
	{
		target = GetNode<Node2D>(TargetPath);
		normalZoom = Zoom;//Guardar Zoom Inicial
	}

	public override void _Process(double delta)
	{
		if (target == null || isZooming) return;//No seguir si está en zoom
		GlobalPosition = target.GlobalPosition;
	}
	
	public void StartZoom()
	{
		if ( isZooming) return;
		isZooming = true; 
		
		var tween = GetTree().CreateTween();
		Vector2 newZoom = new Vector2(2f, 2f);
		
		if(target != null)
		{
			Vector2 centeredPosition = target.GlobalPosition;
			tween.TweenProperty(this, "zoom", newZoom, 0.1f);
			tween.TweenProperty(this, "global_position", centeredPosition, 0.1f);
		}
		tween.SetEase(Tween.EaseType.InOut);
		tween.SetTrans(Tween.TransitionType.Quad);
	}
	
	public void ResetZoom()
	{
	if (!isZooming) return;
	
	isZooming = false;
	
	var tween = GetTree().CreateTween();
	tween.TweenProperty(this, "zoom", normalZoom, 0.3f);
	
	tween.SetEase(Tween.EaseType.InOut);
	tween.SetTrans(Tween.TransitionType.Quad);
	
	GD.Print("Camara simple reseteada");
	}
}
