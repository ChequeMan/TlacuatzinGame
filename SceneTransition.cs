using Godot;
using System;

public partial class SceneTransition : Area2D
{
	// 🎯 Escena a la que quieres ir
	[Export] public string NextScenePath { get; set; } = "res://Scenes/World.tscn";
	
	// 🎨 Mensaje opcional
	[Export] public string TransitionMessage { get; set; } = "Presiona E para entrar";
	
	// ⚙️ Configuración
	[Export] public bool RequireInput { get; set; } = true; // ¿Requiere presionar botón?
	[Export] public string InputAction { get; set; } = "Traspaso"; // Botón para activar
	
	private bool playerInside = false;
	private Label messageLabel;
	
	public override void _Ready()
	{
		// Conectar señales
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
		
		// Crear label para el mensaje (opcional)
		CreateMessageLabel();
		
		GD.Print($"✅ SceneTransition lista. Destino: {NextScenePath}");
	}
	
	private void CreateMessageLabel()
	{
		// Crear un label flotante para mostrar el mensaje
		messageLabel = new Label();
		messageLabel.Text = TransitionMessage;
		messageLabel.Position = new Vector2(-100, -50); // Ajusta según necesites
		messageLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1));
		messageLabel.AddThemeFontSizeOverride("font_size", 16);
		messageLabel.Visible = false;
		AddChild(messageLabel);
	}
	
	private void OnBodyEntered(Node2D body)
	{
		// Verificar si es el jugador
		if (body.IsInGroup("Player"))
		{
			playerInside = true;
			GD.Print("🚪 Jugador entró en zona de transición");
			
			// Mostrar mensaje si está configurado
			if (messageLabel != null && RequireInput)
			{
				messageLabel.Visible = true;
			}
			
			// Si NO requiere input, cambiar escena automáticamente
			if (!RequireInput)
			{
				ChangeScene();
			}
		}
	}
	
	private void OnBodyExited(Node2D body)
	{
		if (body.IsInGroup("Player"))
		{
			playerInside = false;
			GD.Print("🚶 Jugador salió de zona de transición");
			
			// Ocultar mensaje
			if (messageLabel != null)
			{
				messageLabel.Visible = false;
			}
		}
	}
	
	public override void _Process(double delta)
	{
		// Si el jugador está dentro y presiona el botón
		if (playerInside && RequireInput && Input.IsActionJustPressed(InputAction))
		{
			ChangeScene();
		}
	}
	
	private void ChangeScene()
	{
		GD.Print($"🌍 Cambiando a escena: {NextScenePath}");
		
		// Cambiar escena
		GetTree().ChangeSceneToFile(NextScenePath);
	}
}
