using Godot;
using System;

public partial class Camera2d : Camera2D
{
	// --- Configuración básica ---
	[Export] public Node2D Target;
	[Export] public Vector2 Offset = new Vector2(0, -100);

	// Velocidades base y máxima del seguimiento
	[Export] public float BaseFollowSpeed = 5.0f;
	[Export] public float MaxFollowSpeed = 15.0f;

	// Umbral para detectar movimiento rápido (por ejemplo, caída o salto)
	[Export] public float SpeedAdaptThreshold = 300.0f;
	
	private bool isZooming = false;
	
	
	
	private Vector2 normalZoom = new Vector2(1f, 1f);//new

	public override void _Ready()
	{
		// Activa los límites de la cámara
		LimitSmoothed = true;
		//guardar Zoom Inicial
		normalZoom = Zoom; ////new 
	}
	
	
	public void StartZoom(){
		if(isZooming) return;
		ShakeCamera(0.4f, 15f);
		isZooming = true;
		var tween = GetTree().CreateTween();
		
		//nuevo zoom
		Vector2 newZoom = new Vector2(2f, 2f);
		
		// Calcula la posición centrada (sin offset)
		Vector2 centeredPosition = Target.GlobalPosition;
		
		tween.TweenProperty(this, "zoom", newZoom, 0.1f);
		tween.TweenProperty(this,"global_position",centeredPosition, 0.1f);
		
		tween.SetEase(Tween.EaseType.InOut);
		tween.SetTrans(Tween.TransitionType.Quad);
	}
	
	public override void _PhysicsProcess(double delta)
	{
		if (Target == null)
			return;
		if(isZooming)
		return;
		
			
		
		// Calcula la posición deseada (jugador + offset)
		Vector2 desired = Target.GlobalPosition + Offset;

		// --- Adaptación dinámica de la velocidad ---
		float currentSpeed = 0f;

		// Si el Target es un CharacterBody2D, tomamos su velocidad real
		if (Target is CharacterBody2D player)
			currentSpeed = player.Velocity.Length();

		// Calcula la velocidad de seguimiento según la rapidez del jugador
		float t = Mathf.Clamp(currentSpeed / SpeedAdaptThreshold, 0f, 1f);
		float adaptiveSpeed = Mathf.Lerp(BaseFollowSpeed, MaxFollowSpeed, t);
		//GlobalPosition = GlobalPosition.X-50;
		// --- Movimiento suave ---
		GlobalPosition = GlobalPosition.Lerp(desired, (float)delta * adaptiveSpeed);
		
	}
	
	
	public async void ShakeCamera(float duration = 0.3f, float intensity = 10f)
{
	Vector2 originalPosition = GlobalPosition;
	float elapsed = 0f;

	while (elapsed < duration)
	{
		// Genera desplazamiento aleatorio
		float offsetX = (float)GD.RandRange(-intensity, intensity);
		float offsetY = (float)GD.RandRange(-intensity, intensity);

		GlobalPosition = originalPosition + new Vector2(offsetX, offsetY);

		await ToSignal(GetTree(), "physics_frame");
		elapsed += (float)GetProcessDeltaTime();
	}

	// Regresa a la posición original
	GlobalPosition = originalPosition;
}
///metodo nuevo
public void ResetZoom()
{
	if(!isZooming) return;
	
	isZooming = false;
	
	var tween = GetTree().CreateTween();
	tween.TweenProperty(this, "zoom", normalZoom, 0.3f);
	
	tween.SetEase(Tween.EaseType.InOut);
	tween.SetTrans(Tween.TransitionType.Quad);
	
	GD.Print("Camara reseteada");
}
}
