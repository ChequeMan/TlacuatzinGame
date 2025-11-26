using Godot;
using System;
using System.Collections.Generic;

public partial class HealthUI : Control
{
	[Export] public NodePath PlayerPath { get; set; }
	[Export] public int HeartSize { get; set; } = 40; // Tamaño de cada corazón
	[Export] public int HeartSpacing { get; set; } = 10; // Espacio entre corazones
	
	private Player player;
	private List<ColorRect> hearts = new List<ColorRect>();
	private HBoxContainer heartContainer;

	public override void _Ready()
	{
		// Crear contenedor para los corazones
		heartContainer = new HBoxContainer();
		heartContainer.Position = new Vector2(20, 20); // Posición en pantalla
		AddChild(heartContainer);
		
		// Obtener referencia al jugador
		if (PlayerPath != null)
		{
			player = GetNode<Player>(PlayerPath);
		}
		else
		{
			// Buscar al jugador automáticamente
			player = GetTree().Root.GetNode<Player>("World/Player");
		}
		
		if (player == null)
		{
			GD.PrintErr("HealthUI: No se pudo encontrar al Player!");
			return;
		}
		
		// Conectar a la señal de cambio de vida
		player.HealthChanged += OnHealthChanged;
		
		// Inicializar corazones
		InitializeHearts(player.MaxHealth);
		UpdateHearts(player.GetCurrentHealth(), player.MaxHealth);
	}
	
	private void InitializeHearts(int maxHealth)
	{
		// Limpiar corazones anteriores si existen
		foreach (var heart in hearts)
		{
			heart.QueueFree();
		}
		hearts.Clear();
		
		// Crear corazones
		for (int i = 0; i < maxHealth; i++)
		{
			var heart = CreateHeart();
			heartContainer.AddChild(heart);
			hearts.Add(heart);
		}
	}
	
	private ColorRect CreateHeart()
	{
		var heart = new ColorRect();
		heart.CustomMinimumSize = new Vector2(HeartSize, HeartSize);
		heart.Color = new Color(1, 0, 0); // Rojo para corazón lleno
		
		// Agregar un borde para que se vea mejor
		var border = new ColorRect();
		border.CustomMinimumSize = new Vector2(HeartSize, HeartSize);
		border.Color = new Color(0, 0, 0); // Negro para el borde
		border.Position = new Vector2(-2, -2);
		border.Size = new Vector2(HeartSize + 4, HeartSize + 4);
		border.ZIndex = -1;
		
		heart.AddChild(border);
		
		return heart;
	}
	
	private void OnHealthChanged(int currentHealth, int maxHealth)
	{
		UpdateHearts(currentHealth, maxHealth);
	}
	
	private void UpdateHearts(int currentHealth, int maxHealth)
	{
		// Si cambió el máximo de vida, recrear corazones
		if (hearts.Count != maxHealth)
		{
			InitializeHearts(maxHealth);
		}
		
		// Actualizar el color de cada corazón
		for (int i = 0; i < hearts.Count; i++)
		{
			if (i < currentHealth)
			{
				// Corazón lleno (rojo)
				hearts[i].Color = new Color(1, 0, 0);
			}
			else
			{
				// Corazón vacío (gris oscuro)
				hearts[i].Color = new Color(0.3f, 0.3f, 0.3f);
			}
		}
	}
	
	public override void _ExitTree()
	{
		// Desconectar señal al salir
		if (player != null)
		{
			player.HealthChanged -= OnHealthChanged;
		}
	}
}
