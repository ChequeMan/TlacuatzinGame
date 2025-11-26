

using Godot;
using System;

public partial class EnemyEntrenamiento : CharacterBody2D
{
	// ⚙️ Configuración básica
	[Export] public int MaxHealth { get; set; } = 1; // Cambia a 2 o 3 si quieres más golpes
	private int currentHealth;
	
	// 🎨 Referencias a nodos (se asignan automáticamente)
	private Sprite2D sprite;
	private AnimatedSprite2D animSprite;
	
	public override void _Ready()
	{
		// Inicializar vida
		currentHealth = MaxHealth;
		
		// Agregar a grupo para identificación
		AddToGroup("Enemy");
		
		// Obtener referencia al sprite (si existe)
		sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		animSprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		
		GD.Print($"✅ EnemyEntrenamiento listo. Vida: {currentHealth}/{MaxHealth}");
	}

	public override void _PhysicsProcess(double delta)
	{
		// Aplicar gravedad para que el enemigo caiga al suelo
		Vector2 velocity = Velocity;
		
		if (!IsOnFloor())
		{
			velocity += GetGravity() * (float)delta;
		}
		
		Velocity = velocity;
		MoveAndSlide();
	}

	// ⚡ Este método es llamado por el jugador cuando ataca
	public void TakeDamage(int amount, Vector2 knockbackDirection)
	{
		currentHealth -= amount;
		GD.Print($"💥 EnemyEntrenamiento recibió {amount} de daño. Vida: {currentHealth}/{MaxHealth}");
		
		// Efecto visual de daño (parpadeo rojo)
		FlashDamage();
		
		// Si la vida llega a 0, eliminar enemigo
		if (currentHealth <= 0)
		{
			Die();
		}
	}

	// 🔴 Efecto visual de daño
	private async void FlashDamage()
	{
		// Cambiar a color rojo
		if (sprite != null)
			sprite.Modulate = new Color(1, 0.3f, 0.3f);
		if (animSprite != null)
			animSprite.Modulate = new Color(1, 0.3f, 0.3f);
		
		// Esperar un momento
		await ToSignal(GetTree().CreateTimer(0.1f), "timeout");
		
		// Volver al color normal
		if (sprite != null)
			sprite.Modulate = new Color(1, 1, 1);
		if (animSprite != null)
			animSprite.Modulate = new Color(1, 1, 1);
	}

	// 💀 Eliminar el enemigo
	private void Die()
	{
		GD.Print("☠️ EnemyEntrenamiento eliminado");
		
		// Opcional: Reproducir sonido de muerte aquí
		// audioPlayer.Play();
		
		// Opcional: Crear efecto de partículas
		
		// Eliminar de la escena
		QueueFree();
	}
}
