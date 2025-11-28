using Godot;
using System;

public partial class Checkpoint : Area2D
{
	[ExportGroup("Visual Settings")]
	[Export] public bool ShowInGame = true; // Mostrar sprite en el juego
	[Export] public Color InactiveColor = new Color(0, 1, 0, 0.5f); // Verde
	[Export] public Color ActiveColor = new Color(0, 0.5f, 1, 0.7f); // Azul
	
	[ExportGroup("Checkpoint Settings")]
	[Export] public bool OneTimeUse = false; // Si es true, solo se activa una vez
	
	private bool hasBeenActivated = false;
	private Sprite2D visualSprite;
	private AnimationPlayer animPlayer;
	
	public override void _Ready()
	{
		// Configurar Area2D
		Monitoring = true;
		Monitorable = false;
		
		// Crear visual si no existe
		if (GetNode<Sprite2D>("Visual") == null)
		{
			CreateVisual();
		}
		else
		{
			visualSprite = GetNode<Sprite2D>("Visual");
		}
		
		// Verificar AnimationPlayer
		if (HasNode("AnimationPlayer"))
		{
			animPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
		}
		
		// Conectar señal
		BodyEntered += OnBodyEntered;
		
		AddToGroup("Checkpoints");
		
		// Hacer visible/invisible según configuración
		if (visualSprite != null)
		{
			visualSprite.Visible = ShowInGame;
		}
		
		GD.Print($"✅ Checkpoint listo en: {GlobalPosition}");
	}
	
	private void CreateVisual()
	{
		// Crear sprite simple
		visualSprite = new Sprite2D();
		visualSprite.Name = "Visual";
		
		// Crear textura simple (cuadrado)
		var texture = new GradientTexture2D();
		var gradient = new Gradient();
		gradient.SetColor(0, InactiveColor);
		gradient.SetColor(1, new Color(InactiveColor.R, InactiveColor.G, InactiveColor.B, 0));
		texture.Gradient = gradient;
		texture.Width = 64;
		texture.Height = 128;
		texture.Fill = GradientTexture2D.FillEnum.Radial;
		
		visualSprite.Texture = texture;
		visualSprite.Centered = true;
		AddChild(visualSprite);
	}
	
	private void OnBodyEntered(Node2D body)
	{
		// Solo activar si es el jugador
		if (!body.IsInGroup("Player")) return;
		
		// Si ya fue activado y es de un solo uso, no hacer nada
		if (hasBeenActivated && OneTimeUse) return;
		
		// Activar checkpoint
		hasBeenActivated = true;
		
		// Registrar en el manager
		if (CheckpointManager.Instance != null)
		{
			CheckpointManager.Instance.RegisterCheckpoint(GlobalPosition);
		}
		else
		{
			GD.PrintErr("⚠️ CheckpointManager no encontrado! Asegúrate de agregarlo como Autoload.");
		}
		
		// Feedback visual
		PlayActivationFeedback();
		
		GD.Print($"🎯 ¡Checkpoint activado en {GlobalPosition}!");
	}
	
	private async void PlayActivationFeedback()
	{
		if (visualSprite == null) return;
		
		// Animación de activación
		if (animPlayer != null && animPlayer.HasAnimation("activate"))
		{
			animPlayer.Play("activate");
		}
		else
		{
			// Animación simple por código
			// Parpadeo
			for (int i = 0; i < 3; i++)
			{
				visualSprite.Modulate = new Color(1, 1, 0, 1); // Amarillo
				await ToSignal(GetTree().CreateTimer(0.1f), "timeout");
				visualSprite.Modulate = Colors.White;
				await ToSignal(GetTree().CreateTimer(0.1f), "timeout");
			}
			
			// Cambiar a color activado
			if (visualSprite.Texture is GradientTexture2D gradTex)
			{
				gradTex.Gradient.SetColor(0, ActiveColor);
			}
			
			// Escala pulse
			var originalScale = visualSprite.Scale;
			var tween = CreateTween();
			tween.TweenProperty(visualSprite, "scale", originalScale * 1.3f, 0.2f);
			tween.TweenProperty(visualSprite, "scale", originalScale, 0.2f);
		}
	}
	
	// Resetear el checkpoint (útil para testing o niveles con checkpoints reutilizables)
	public void ResetCheckpoint()
	{
		hasBeenActivated = false;
		
		if (visualSprite != null && visualSprite.Texture is GradientTexture2D gradTex)
		{
			gradTex.Gradient.SetColor(0, InactiveColor);
		}
		
		GD.Print($"🔄 Checkpoint en {GlobalPosition} reseteado");
	}
}
