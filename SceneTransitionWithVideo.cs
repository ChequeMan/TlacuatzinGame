using Godot;
using System;

public partial class SceneTransitionWithVideo : Area2D
{
	// 🎬 CONFIGURACIÓN DE VIDEO
	[ExportGroup("Video Settings")]
	[Export] public bool PlayVideo { get; set; } = false; // Activar para reproducir video
	[Export] public string VideoPath { get; set; } = "res://Videos/ending.ogv"; // Ruta del video
	
	// 🎯 CONFIGURACIÓN DE ESCENA
	[ExportGroup("Scene Settings")]
	[Export] public string NextScenePath { get; set; } = "res://Scenes/MainMenu.tscn"; // A dónde ir después del video
	
	// 🎨 MENSAJE
	[ExportGroup("Message Settings")]
	[Export] public string TransitionMessage { get; set; } = "Presiona E para continuar";
	
	// ⚙️ CONFIGURACIÓN DE INTERACCIÓN
	[ExportGroup("Interaction Settings")]
	[Export] public bool RequireInput { get; set; } = true; // ¿Requiere presionar botón?
	[Export] public string InputAction { get; set; } = "Traspaso"; // Botón para activar
	[Export] public bool CanSkipVideo { get; set; } = true; // Permitir saltar video
	
	private bool playerInside = false;
	private Label messageLabel;
	private VideoStreamPlayer videoPlayer;
	private bool isPlayingVideo = false;
	
	public override void _Ready()
	{
		// Conectar señales
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
		
		// Crear label para el mensaje
		CreateMessageLabel();
		
		GD.Print($"✅ SceneTransition lista. Video: {(PlayVideo ? VideoPath : "No")}");
	}
	
	private void CreateMessageLabel()
	{
		messageLabel = new Label();
		messageLabel.Text = TransitionMessage;
		messageLabel.Position = new Vector2(-100, -50);
		messageLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1));
		messageLabel.AddThemeFontSizeOverride("font_size", 16);
		messageLabel.Visible = false;
		AddChild(messageLabel);
	}
	
	private void OnBodyEntered(Node2D body)
	{
		if (body.IsInGroup("Player"))
		{
			playerInside = true;
			GD.Print("🚪 Jugador entró en zona de transición");
			
			if (messageLabel != null && RequireInput)
			{
				messageLabel.Visible = true;
			}
			
			// Si NO requiere input, activar automáticamente
			if (!RequireInput)
			{
				TriggerTransition();
			}
		}
	}
	
	private void OnBodyExited(Node2D body)
	{
		if (body.IsInGroup("Player"))
		{
			playerInside = false;
			GD.Print("🚶 Jugador salió de zona de transición");
			
			if (messageLabel != null)
			{
				messageLabel.Visible = false;
			}
		}
	}
	
	public override void _Process(double delta)
	{
		// Activar transición con input
		if (playerInside && RequireInput && Input.IsActionJustPressed(InputAction) && !isPlayingVideo)
		{
			TriggerTransition();
		}
		
		// Saltar video con cualquier tecla
		if (isPlayingVideo && CanSkipVideo && Input.IsActionJustPressed("ui_accept"))
		{
			SkipVideo();
		}
	}
	
	private void TriggerTransition()
	{
		if (PlayVideo)
		{
			PlayCinematic();
		}
		else
		{
			ChangeScene();
		}
	}
	
	// ========== REPRODUCCIÓN DE VIDEO ==========
	private void PlayCinematic()
	{
		GD.Print($"🎬 Reproduciendo cinemática: {VideoPath}");
		
		// Ocultar mensaje
		if (messageLabel != null)
			messageLabel.Visible = false;
		
		// Crear VideoStreamPlayer
		videoPlayer = new VideoStreamPlayer();
		videoPlayer.Name = "CinematicPlayer";
		
		// Cargar video
		VideoStream videoStream = GD.Load<VideoStream>(VideoPath);
		if (videoStream == null)
		{
			GD.PrintErr($"❌ No se pudo cargar el video: {VideoPath}");
			ChangeScene();
			return;
		}
		
		videoPlayer.Stream = videoStream;
		
		// Configurar video en pantalla completa
		videoPlayer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		videoPlayer.Autoplay = false;
		
		// Añadir al CanvasLayer para que esté encima de todo
		CanvasLayer cinematicLayer = new CanvasLayer();
		cinematicLayer.Name = "CinematicLayer";
		cinematicLayer.Layer = 100; // Capa alta para estar encima
		GetTree().Root.AddChild(cinematicLayer);
		cinematicLayer.AddChild(videoPlayer);
		
		// Añadir label de "Skip" (opcional)
		if (CanSkipVideo)
		{
			Label skipLabel = new Label();
			skipLabel.Text = "Presiona ESPACIO para saltar";
			skipLabel.Position = new Vector2(20, 20);
			skipLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.7f));
			skipLabel.AddThemeFontSizeOverride("font_size", 14);
			cinematicLayer.AddChild(skipLabel);
		}
		
		// Conectar señal de finalización
		videoPlayer.Finished += OnVideoFinished;
		
		// Pausar juego (opcional)
		GetTree().Paused = true;
		videoPlayer.ProcessMode = ProcessModeEnum.Always; // El video sigue reproduciéndose aunque el juego esté pausado
		
		// Reproducir
		videoPlayer.Play();
		isPlayingVideo = true;
		
		GD.Print("▶️ Video iniciado");
	}
	
	private void OnVideoFinished()
	{
		GD.Print("✅ Video terminado");
		CleanupVideo();
		ChangeScene();
	}
	
	private void SkipVideo()
	{
		GD.Print("⏭️ Video saltado");
		CleanupVideo();
		ChangeScene();
	}
	
	private void CleanupVideo()
	{
		isPlayingVideo = false;
		
		// Despausar juego
		GetTree().Paused = false;
		
		// Eliminar player y layer
		if (videoPlayer != null)
		{
			var layer = videoPlayer.GetParent();
			if (layer != null)
			{
				layer.QueueFree();
			}
		}
	}
	
	private void ChangeScene()
	{
		GD.Print($"🌍 Cambiando a escena: {NextScenePath}");
		GetTree().ChangeSceneToFile(NextScenePath);
	}
}
