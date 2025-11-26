using Godot;
using System;

public partial class BossHealthUI : Control
{
	// ========== CONFIGURACIÓN ==========
	[Export] public NodePath BossPath { get; set; }
	[Export] public string BossName { get; set; } = "BOSS FINAL";
	[Export] public Color HealthBarFillColor { get; set; } = new Color(0.8f, 0.1f, 0.1f); // Rojo
	[Export] public Color HealthBarBackgroundColor { get; set; } = new Color(0.2f, 0.2f, 0.2f); // Gris oscuro
	[Export] public Color HealthBarBorderColor { get; set; } = new Color(1, 1, 1); // Blanco
	[Export] public float BarHeight { get; set; } = 35f;
	[Export] public float BarWidth { get; set; } = 600f;
	[Export] public float AnimationSpeed { get; set; } = 5f; // Velocidad de animación de reducción
	
	// ========== COMPONENTES ==========
	private FinalBoss boss;
	private ProgressBar healthBar;
	private Label nameLabel;
	private Label healthLabel;
	private Panel backgroundPanel;
	private ColorRect barBorder;
	
	private float targetHealthPercent = 1.0f;
	private bool isVisible = false;
	
	public override void _Ready()
	{
		// Ocultar inicialmente
		Visible = false;
		
		// Crear estructura de UI
		CreateUI();
		
		// Obtener referencia al boss
		if (BossPath != null)
		{
			boss = GetNode<FinalBoss>(BossPath);
		}
		else
		{
			// Buscar al boss automáticamente
			boss = GetTree().GetFirstNodeInGroup("Boss") as FinalBoss;
		}
		
		if (boss == null)
		{
			GD.PrintErr("BossHealthUI: No se pudo encontrar al Boss!");
			return;
		}
		
		// Conectar señales
		boss.HealthChanged += OnBossHealthChanged;
		boss.BossDefeated += OnBossDefeated;
		
		// Inicializar valores
		UpdateHealthBar(boss.MaxHealth, boss.MaxHealth, true);
	}
	
	private void CreateUI()
	{
		// Configurar el control principal para que esté centrado arriba
		AnchorLeft = 0.5f;
		AnchorRight = 0.5f;
		AnchorTop = 0f;
		AnchorBottom = 0f;
		
		OffsetLeft = -BarWidth / 2;
		OffsetRight = BarWidth / 2;
		OffsetTop = 30;
		OffsetBottom = 100;
		
		// Panel de fondo (decorativo)
		backgroundPanel = new Panel();
		backgroundPanel.AnchorLeft = 0;
		backgroundPanel.AnchorRight = 1;
		backgroundPanel.AnchorTop = 0;
		backgroundPanel.AnchorBottom = 1;
		
		// Crear un StyleBox para el panel
		var panelStyle = new StyleBoxFlat();
		panelStyle.BgColor = new Color(0, 0, 0, 0.7f); // Fondo semi-transparente
		panelStyle.BorderWidthLeft = 2;
		panelStyle.BorderWidthRight = 2;
		panelStyle.BorderWidthTop = 2;
		panelStyle.BorderWidthBottom = 2;
		panelStyle.BorderColor = HealthBarBorderColor;
		panelStyle.CornerRadiusTopLeft = 5;
		panelStyle.CornerRadiusTopRight = 5;
		panelStyle.CornerRadiusBottomLeft = 5;
		panelStyle.CornerRadiusBottomRight = 5;
		backgroundPanel.AddThemeStyleboxOverride("panel", panelStyle);
		
		AddChild(backgroundPanel);
		
		// Nombre del boss
		nameLabel = new Label();
		nameLabel.Text = BossName;
		nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
		nameLabel.Position = new Vector2(0, 5);
		nameLabel.Size = new Vector2(BarWidth, 20);
		nameLabel.AddThemeFontSizeOverride("font_size", 18);
		nameLabel.AddThemeColorOverride("font_color", Colors.White);
		AddChild(nameLabel);
		
		// Barra de vida
		healthBar = new ProgressBar();
		healthBar.Position = new Vector2(10, 30);
		healthBar.Size = new Vector2(BarWidth - 20, BarHeight);
		healthBar.MinValue = 0;
		healthBar.MaxValue = 100;
		healthBar.Value = 100;
		healthBar.ShowPercentage = false;
		
		// Estilizar la barra
		var barBackground = new StyleBoxFlat();
		barBackground.BgColor = HealthBarBackgroundColor;
		barBackground.CornerRadiusTopLeft = 3;
		barBackground.CornerRadiusTopRight = 3;
		barBackground.CornerRadiusBottomLeft = 3;
		barBackground.CornerRadiusBottomRight = 3;
		
		var barFill = new StyleBoxFlat();
		barFill.BgColor = HealthBarFillColor;
		barFill.CornerRadiusTopLeft = 3;
		barFill.CornerRadiusTopRight = 3;
		barFill.CornerRadiusBottomLeft = 3;
		barFill.CornerRadiusBottomRight = 3;
		
		healthBar.AddThemeStyleboxOverride("background", barBackground);
		healthBar.AddThemeStyleboxOverride("fill", barFill);
		
		AddChild(healthBar);
		
		// Label de vida (números)
		healthLabel = new Label();
		healthLabel.HorizontalAlignment = HorizontalAlignment.Center;
		healthLabel.Position = new Vector2(0, 30);
		healthLabel.Size = new Vector2(BarWidth, BarHeight);
		healthLabel.AddThemeFontSizeOverride("font_size", 16);
		healthLabel.AddThemeColorOverride("font_color", Colors.White);
		// Agregar sombra al texto para que se vea mejor
		healthLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
		healthLabel.AddThemeConstantOverride("outline_size", 2);
		AddChild(healthLabel);
	}
	
	private void OnBossHealthChanged(int currentHealth, int maxHealth)
	{
		// Mostrar la barra cuando el boss recibe daño por primera vez
		if (!isVisible && currentHealth < maxHealth)
		{
			isVisible = true;
			Visible = true;
			// Animación de entrada (opcional)
			AnimateIn();
		}
		
		UpdateHealthBar(currentHealth, maxHealth);
	}
	
	private void UpdateHealthBar(int currentHealth, int maxHealth, bool instant = false)
	{
		float percent = (float)currentHealth / maxHealth * 100f;
		targetHealthPercent = percent;
		
		if (instant)
		{
			healthBar.Value = percent;
		}
		
		// Actualizar texto
		healthLabel.Text = $"{currentHealth} / {maxHealth}";
		
		// Cambiar color según vida restante
		UpdateBarColor(percent);
	}
	
	private void UpdateBarColor(float percent)
	{
		Color newColor;
		
		if (percent > 66)
		{
			// Verde/Amarillo cuando tiene mucha vida
			newColor = HealthBarFillColor;
		}
		else if (percent > 33)
		{
			// Naranja en fase media
			newColor = new Color(1.0f, 0.5f, 0.1f);
		}
		else
		{
			// Rojo intenso cuando está por morir
			newColor = new Color(1.0f, 0.1f, 0.1f);
		}
		
		var barFill = new StyleBoxFlat();
		barFill.BgColor = newColor;
		barFill.CornerRadiusTopLeft = 3;
		barFill.CornerRadiusTopRight = 3;
		barFill.CornerRadiusBottomLeft = 3;
		barFill.CornerRadiusBottomRight = 3;
		healthBar.AddThemeStyleboxOverride("fill", barFill);
	}
	
	private async void AnimateIn()
	{
		// Animación de entrada desde arriba
		float originalTop = OffsetTop;
		OffsetTop = -100;
		
		float duration = 0.5f;
		float elapsed = 0f;
		
		while (elapsed < duration)
		{
			elapsed += (float)GetProcessDeltaTime();
			float t = elapsed / duration;
			OffsetTop = Mathf.Lerp(-100, originalTop, t);
			await ToSignal(GetTree(), "process_frame");
		}
		
		OffsetTop = originalTop;
	}
	
	private void OnBossDefeated()
	{
		GD.Print("🎉 ¡Boss derrotado! Ocultando barra de vida...");
		AnimateOut();
	}
	
	private async void AnimateOut()
	{
		// Animación de salida
		float duration = 1.0f;
		float elapsed = 0f;
		
		while (elapsed < duration)
		{
			elapsed += (float)GetProcessDeltaTime();
			float t = elapsed / duration;
			Modulate = new Color(1, 1, 1, 1 - t);
			await ToSignal(GetTree(), "process_frame");
		}
		
		Visible = false;
	}
	
	public override void _Process(double delta)
	{
		// Animar suavemente la reducción de la barra
		if (Mathf.Abs(healthBar.Value - targetHealthPercent) > 0.1f)
		{
			healthBar.Value = Mathf.Lerp(healthBar.Value, targetHealthPercent, AnimationSpeed * (float)delta);
		}
		else
		{
			healthBar.Value = targetHealthPercent;
		}
	}
	
	public override void _ExitTree()
	{
		// Desconectar señales
		if (boss != null)
		{
			boss.HealthChanged -= OnBossHealthChanged;
			boss.BossDefeated -= OnBossDefeated;
		}
	}
}
