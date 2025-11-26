using Godot;
using System;

public partial class BossProjectile : Area2D
{
	// ========== CONFIGURACIÓN ==========
	[Export] public float Speed = 300f;
	//[Export] public int Damage = 1;
	[Export] public float Lifetime = 5f; // Segundos antes de auto-destruirse
	[Export] public bool HomingEnabled = false; // Seguir al jugador
	[Export] public float HomingStrength = 2f; // Qué tan fuerte sigue al jugador
	[Export] public bool RotateWithDirection = true; // Rotar el sprite según dirección
	private int _damageToApply = 1;
	
	// ========== EFECTOS VISUALES ==========
	[Export] public bool TrailEnabled = true;
	[Export] public Color TrailColor = new Color(1, 0.3f, 0.3f, 0.5f);
	[Export] public PackedScene ImpactEffect; // Efecto de partículas al impactar
	
	// ========== COMPONENTES ==========
	private AnimatedSprite2D anim;
	private CollisionShape2D collisionShape;
	private GpuParticles2D trail;
	
	// ========== VARIABLES DE MOVIMIENTO ==========
	private Vector2 _direction = Vector2.Right;
	private float _currentSpeed = 0f;
	private float _lifetimeTimer = 0f;
	private CharacterBody2D _target;
	private bool _hasHit = false;
	
	public override void _Ready()
	{
		// Obtener componentes
		anim = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		collisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
		trail = GetNodeOrNull<GpuParticles2D>("Trail");
		
		// Configurar señales
		BodyEntered += OnBodyEntered;
		AreaEntered += OnAreaEntered;
		
		// Iniciar animación
		if (anim != null)
		{
			anim.Play("ProjectileMove");
		}
		
		// Configurar trail
		if (trail != null && TrailEnabled)
		{
			trail.Emitting = true;
			// Configurar color del trail
			var material = trail.ProcessMaterial as ParticleProcessMaterial;
			if (material != null)
			{
				material.Color = TrailColor;
			}
		}
		
		_lifetimeTimer = Lifetime;
		_currentSpeed = Speed;
		
		// Buscar jugador si homing está activado
		if (HomingEnabled)
		{
			_target = GetTree().GetFirstNodeInGroup("Player") as CharacterBody2D;
		}
		
		AddToGroup("EnemyProjectile");
	}
	
	// ========== MÉTODO LLAMADO POR EL BOSS ==========
	public void SetDirection(Vector2 direction, float speed)
	{
		_direction = direction.Normalized();
		_currentSpeed = speed;
		
		// Rotar el sprite para que apunte en la dirección correcta
		if (RotateWithDirection)
		{
			Rotation = _direction.Angle();
		}
	}
	
	public void SetDamage(int damage)
	{
		_damageToApply = damage;
	}
	
	// ========== DETECCIÓN DE COLISIONES ==========
	private void OnBodyEntered(Node2D body)
	{
		if (_hasHit) return;
		
		// Golpear al jugador
		if (body.IsInGroup("Player"))
		{
			Player player = body as Player;
			if (player != null)
			{
				//player.Die();
				player.TakeDamage(_damageToApply); // ✅ Usar el daño asignado
				GD.Print("🎯 Proyectil del Boss golpeó al jugador!");
			}
			
			HitTarget();
		}
		// Golpear paredes/obstáculos
		else if (body is TileMap || body is StaticBody2D)
		{
			GD.Print("💥 Proyectil golpeó pared");
			HitTarget();
		}
	}
	
	private void OnAreaEntered(Area2D area)
	{
		if (_hasHit) return;
		
		// Opcional: Si el jugador tiene un shield o área de defensa
		if (area.IsInGroup("PlayerShield"))
		{
			GD.Print("🛡️ Proyectil bloqueado por escudo!");
			HitTarget();
		}
	}
	
	// ========== COMPORTAMIENTO AL IMPACTAR ==========
	private void HitTarget()
	{
		if (_hasHit) return;
		_hasHit = true;
		
		// Desactivar colisión
		collisionShape.SetDeferred("disabled", true);
		
		// Detener movimiento
		_currentSpeed = 0;
		
		// Spawn efecto de impacto
		if (ImpactEffect != null)
		{
			var effect = ImpactEffect.Instantiate<Node2D>();
			GetTree().Root.AddChild(effect);
			effect.GlobalPosition = GlobalPosition;
		}
		
		// Ocultar sprite y trail
		if (anim != null)
		{
			anim.Visible = false;
		}
		if (trail != null)
		{
			trail.Emitting = false;
		}
		
		// Destruir después de un breve delay (para que el efecto se vea)
		GetTree().CreateTimer(0.3).Timeout += QueueFree;
	}
	
	// ========== ACTUALIZACIÓN ==========
	public override void _PhysicsProcess(double delta)
	{
		if (_hasHit) return;
		
		float d = (float)delta;
		
		// Reducir lifetime
		_lifetimeTimer -= d;
		if (_lifetimeTimer <= 0)
		{
			GD.Print("⏰ Proyectil expiró");
			QueueFree();
			return;
		}
		
		// Homing (seguir al jugador)
		if (HomingEnabled && _target != null && IsInstanceValid(_target))
		{
			Vector2 targetDirection = ((_target.GlobalPosition + new Vector2(0, -20)) - GlobalPosition).Normalized();
			_direction = _direction.Lerp(targetDirection, HomingStrength * d).Normalized();
			
			// Actualizar rotación si está activado
			if (RotateWithDirection)
			{
				Rotation = _direction.Angle();
			}
		}
		
		// Mover el proyectil
		Vector2 velocity = _direction * _currentSpeed;
		GlobalPosition += velocity * d;
		
		// Opcional: Parpadeo cuando está por expirar
		if (_lifetimeTimer < 1f && anim != null)
		{
			float flash = Mathf.Sin(_lifetimeTimer * 20f);
			anim.Modulate = new Color(1, 1, 1, Mathf.Abs(flash));
		}
	}
	
	// ========== MÉTODO PARA CAMBIAR COMPORTAMIENTO EN RUNTIME ==========
	public void EnableHoming(CharacterBody2D target = null)
	{
		HomingEnabled = true;
		if (target != null)
		{
			_target = target;
		}
		else
		{
			_target = GetTree().GetFirstNodeInGroup("Player") as CharacterBody2D;
		}
		GD.Print("🎯 Homing activado!");
	}
	
	public void DisableHoming()
	{
		HomingEnabled = false;
		_target = null;
	}
	
	public void SetSpeed(float newSpeed)
	{
		_currentSpeed = newSpeed;
		Speed = newSpeed;
	}
}
