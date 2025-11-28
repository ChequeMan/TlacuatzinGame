using Godot;
using System;

public partial class EnemyBird : CharacterBody2D
{
	// Propiedades de movimiento
	[Export] public int FlySpeed = 150;
	[Export] public int ChaseSpeed = 250;
	[Export] public float PatrolRadius = 200f;
	[Export] public float HoverAmplitude = 30f;
	[Export] public float HoverFrequency = 2f;
	
	// Propiedades de combate
	[Export] public float AggroRange = 350f;
	[Export] public float AttackRange = 80f;
	[Export] public int MaxHealth = 2;
	[Export] public int AttackDamage = 1;
	[Export] public float KnockbackForce = 150f;
	[Export] public float StunDuration = 0.3f;
	
	// Ataque en picada
	[Export] public float DiveSpeed = 400f;
	[Export] public float DivePrepTime = 0.5f;
	[Export] public float DiveRecoveryTime = 1f;
	[Export] public float AttackCooldown = 2f;
	[Export] public float RetreatHeight = 200f;
	[Export] public float RetreatSpeed = 250f;
	
	// Patrón de vuelo
	public enum FlightPattern { Horizontal, Circular, Hovering }
	[Export] public FlightPattern PatrolPattern = FlightPattern.Circular;
	
	private Vector2 _startPosition;
	private bool _movingRight = true;
	private CharacterBody2D _player;
	private bool _isChasing = false;
	
	// Componentes
	private AnimatedSprite2D anim;
	private Area2D AlertBox;
	private Area2D AttackBox;
	private Area2D HurtBox;
	
	// Sistema de vida
	private int _currentHealth;
	private bool _isDead = false;
	private bool _isHurt = false;
	private float _hurtTimer = 0f;
	private float _hurtDuration = 0.4f;
	
	// Sistema de ataque
	private bool _isAttacking = false;
	private bool _isDiving = false;
	private bool _isPreparingDive = false;
	private bool _isRetreating = false;
	private bool _attackHitPlayer = false;
	private Vector2 _retreatTarget;
	private float _diveTimer = 0f;
	private float _attackCooldownTimer = 0f;
	private bool _canAttack = true;
	private Vector2 _diveTarget;
	
	// Sistema de movimiento
	private Vector2 _knockbackVelocity = Vector2.Zero;
	private bool _isKnockedBack = false;
	private float _detectionTimer = 0f;
	private bool _playerSpotted = false;
	private bool _facingRight = true;
	
	// Variables para patrón de vuelo
	private float _patrolAngle = 0f;
	private float _hoverTime = 0f;
	
	public override void _Ready()
	{
		anim = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_startPosition = GlobalPosition;
		_currentHealth = MaxHealth;
		
		// Configurar AlertBox
		AlertBox = GetNode<Area2D>("AlertBox");
		AlertBox.BodyEntered += OnBodyEntered;
		AlertBox.BodyExited += OnBodyExited;
		
		// Configurar AttackBox
		AttackBox = GetNode<Area2D>("Attackbox");
		AttackBox.Monitoring = false;
		AttackBox.BodyEntered += OnAttackBoxBodyEntered;
		
		// Configurar HurtBox
		HurtBox = GetNode<Area2D>("Hurtbox");
		HurtBox.BodyEntered += OnHurtBoxBodyEntered;
		
		// Eventos de animación
		anim.AnimationFinished += OnAnimationFinished;
		
		GD.Print($"Pájaro enemigo inicializado - Patrón: {PatrolPattern}");
	}
	
	private void OnHurtBoxBodyEntered(Node2D body)
	{
		if (body.IsInGroup("Player") && !_isDead && !_isHurt)
		{
			Player player = body as Player;
			if (player != null && player.IsAttacking())
			{
				Vector2 knockbackDirection = (GlobalPosition - player.GlobalPosition).Normalized();
				TakeDamage(1, knockbackDirection);
			}
		}
	}
	
	public void TakeDamage(int damage, Vector2 knockbackDirection)
	{
		if (_isDead || _isHurt) return;
		
		_currentHealth -= damage;
		GD.Print($"¡Pájaro golpeado! Vida restante: {_currentHealth}/{MaxHealth}");
		
		// Aplicar knockback hacia arriba y atrás
		Vector2 upwardKnockback = new Vector2(knockbackDirection.X, -1).Normalized();
		_knockbackVelocity = upwardKnockback * KnockbackForce;
		_isKnockedBack = true;
		
		// Cancelar ataque y empezar retirada
		_isAttacking = false;
		_isDiving = false;
		_isPreparingDive = false;
		AttackBox.Monitoring = false;
		
		// Iniciar retirada hacia arriba
		_isRetreating = true;
		_retreatTarget = new Vector2(GlobalPosition.X, _startPosition.Y - RetreatHeight);
		
		// Resetear cooldown
		_canAttack = false;
		_attackCooldownTimer = AttackCooldown * 0.5f;
		
		// Activar persecución si hay jugador
		if (_player != null)
		{
			_playerSpotted = true;
			_isChasing = true;
		}
		
		if (_currentHealth <= 0)
		{
			Die();
		}
		else
		{
			_isHurt = true;
			_hurtTimer = _hurtDuration;
			anim.Play("hurt");
			FlashDamage();
			GD.Print("🦅 Pájaro herido, volando alto!");
		}
	}
	
	private async void FlashDamage()
	{
		Modulate = new Color(1, 0.3f, 0.3f);
		await ToSignal(GetTree().CreateTimer(0.1), "timeout");
		Modulate = Colors.White;
	}
	
	private void Die()
	{
		if (_isDead) return;
		
		_isDead = true;
		_isChasing = false;
		_isAttacking = false;
		_isDiving = false;
		_isPreparingDive = false;
		
		// Desactivar detección
		AttackBox.Monitoring = false;
		AlertBox.Monitoring = false;
		HurtBox.Monitoring = false;
		HurtBox.Monitorable = false;
		
		// Desactivar colisiones
		SetCollisionLayerValue(1, false);
		SetCollisionMaskValue(1, false);
		
		anim.Play("death");
		
		// Caída con gravedad
		Velocity = new Vector2(_knockbackVelocity.X * 0.5f, -100);
		
		GD.Print("¡Pájaro eliminado!");
	}
	
	private void OnAttackBoxBodyEntered(Node2D body)
	{
		if (body.IsInGroup("Player") && !_isDead)
		{
			Player player = body as Player;
			if (player != null)
			{
				GD.Print($"💥 Pájaro golpeó al jugador causando {AttackDamage} de daño");
				player.TakeDamage(AttackDamage);
				_attackHitPlayer = true;
			}
		}
	}
	
	private void OnBodyEntered(Node2D body)
	{
		if (body.IsInGroup("Player") && !_isDead)
		{
			_playerSpotted = true;
			_isChasing = true;
			_player = body as CharacterBody2D;
			_detectionTimer = 0f;
			GD.Print("¡Pájaro detectó al jugador!");
		}
	}
	
	private void OnBodyExited(Node2D body)
	{
		if (body.IsInGroup("Player"))
		{
			_detectionTimer = 3f;
		}
	}
	
	private void OnAnimationFinished()
	{
		if (anim.Animation == "dive" || anim.Animation == "attack")
		{
			_isAttacking = false;
			_isDiving = false;
			AttackBox.Monitoring = false;
			
			// Verificar si el ataque conectó
			if (_attackHitPlayer)
			{
				// Si golpeó, cooldown normal y vuela alto
				_attackCooldownTimer = AttackCooldown;
				_canAttack = false;
				_attackHitPlayer = false;
				
				_isRetreating = true;
				_retreatTarget = new Vector2(GlobalPosition.X, _startPosition.Y - RetreatHeight);
				GD.Print("✅ Ataque exitoso, volando alto!");
			}
			else
			{
				// Si falló, intenta de nuevo rápidamente
				_attackCooldownTimer = 0.3f;
				_canAttack = false;
				
				_isRetreating = true;
				_retreatTarget = new Vector2(GlobalPosition.X, _startPosition.Y - RetreatHeight * 0.6f);
				GD.Print("❌ Ataque falló, reintentando rápido!");
			}
		}
		else if (anim.Animation == "hurt")
		{
			_isHurt = false;
		}
		else if (anim.Animation == "death")
		{
			QueueFree();
		}
	}
	
	private void UpdateFacingDirection(bool shouldFaceRight)
	{
		_facingRight = shouldFaceRight;
		anim.FlipH = _facingRight;
		
		float scaleX = _facingRight ? -1 : 1;
		AlertBox.Scale = new Vector2(scaleX, 1);
		AttackBox.Scale = new Vector2(scaleX, 1);
		HurtBox.Scale = new Vector2(scaleX, 1);
	}
	
	private Vector2 GetPatrolPosition(float delta)
	{
		_hoverTime += delta;
		
		switch (PatrolPattern)
		{
			case FlightPattern.Horizontal:
				float hoverOffset = Mathf.Sin(_hoverTime * HoverFrequency) * HoverAmplitude;
				
				if (_movingRight)
				{
					if (GlobalPosition.X < _startPosition.X + PatrolRadius)
					{
						return new Vector2(FlySpeed, hoverOffset * 10);
					}
					else
					{
						_movingRight = false;
					}
				}
				else
				{
					if (GlobalPosition.X > _startPosition.X - PatrolRadius)
					{
						return new Vector2(-FlySpeed, hoverOffset * 10);
					}
					else
					{
						_movingRight = true;
					}
				}
				break;
				
			case FlightPattern.Circular:
				_patrolAngle += delta * 0.5f;
				Vector2 circlePos = _startPosition + new Vector2(
					Mathf.Cos(_patrolAngle) * PatrolRadius,
					Mathf.Sin(_patrolAngle) * PatrolRadius * 0.5f
				);
				Vector2 direction = (circlePos - GlobalPosition).Normalized();
				return direction * FlySpeed;
				
			case FlightPattern.Hovering:
				Vector2 toStart = (_startPosition - GlobalPosition);
				float hover = Mathf.Sin(_hoverTime * HoverFrequency) * HoverAmplitude;
				return new Vector2(toStart.X * 0.5f, hover);
		}
		
		return Vector2.Zero;
	}
	
	public override void _PhysicsProcess(double delta)
	{
		// Si está muerto, solo aplicar gravedad
		if (_isDead)
		{
			Velocity = new Vector2(Velocity.X, Velocity.Y + 500 * (float)delta);
			MoveAndSlide();
			return;
		}
		
		// Verificar si está cayendo y tocó el suelo o colisionó
		if (_isKnockedBack && IsOnFloor())
		{
			_isKnockedBack = false;
			_isRetreating = true;
			_retreatTarget = new Vector2(GlobalPosition.X, _startPosition.Y - RetreatHeight);
			GD.Print("🦅 Pájaro tocó el suelo, subiendo!");
		}
		
		// Verificar colisión con paredes durante knockback
		if (_isKnockedBack && IsOnWall())
		{
			_isKnockedBack = false;
			_knockbackVelocity = Vector2.Zero;
			_isRetreating = true;
			_retreatTarget = new Vector2(GlobalPosition.X, _startPosition.Y - RetreatHeight);
			GD.Print("🦅 Pájaro chocó con pared, subiendo!");
		}
		
		// Actualizar cooldowns
		if (!_canAttack)
		{
			_attackCooldownTimer -= (float)delta;
			if (_attackCooldownTimer <= 0)
			{
				_canAttack = true;
			}
		}
		
		if (_diveTimer > 0)
		{
			_diveTimer -= (float)delta;
		}
		
		// Actualizar temporizador de detección
		if (_detectionTimer > 0)
		{
			_detectionTimer -= (float)delta;
			if (_detectionTimer <= 0)
			{
				_isChasing = false;
				_player = null;
				_playerSpotted = false;
			}
		}
		
		// Manejo de estado herido
		if (_isHurt)
		{
			_hurtTimer -= (float)delta;
			if (_hurtTimer <= 0)
			{
				_isHurt = false;
			}
			
			// Durante el estado herido, volar hacia arriba
			if (_isRetreating)
			{
				Vector2 directionToRetreat = (_retreatTarget - GlobalPosition).Normalized();
				Velocity = directionToRetreat * RetreatSpeed;
				
				// Si llegó a la altura de retirada
				if (GlobalPosition.Y <= _retreatTarget.Y)
				{
					_isRetreating = false;
					GD.Print("✅ Pájaro alcanzó altura segura");
				}
			}
			else if (_isKnockedBack)
			{
				// Aplicar knockback con gravedad
				Velocity = _knockbackVelocity;
				_knockbackVelocity.Y += 500 * (float)delta;
				_knockbackVelocity.X = Mathf.Lerp(_knockbackVelocity.X, 0, 0.1f);
			}
			
			MoveAndSlide();
			return;
		}
		
		// Comportamiento de persecución y ataque
		if (_isChasing && _player != null)
		{
			float distanceToPlayer = GlobalPosition.DistanceTo(_player.GlobalPosition);
			
			// Actualizar dirección
			bool shouldFaceRight = _player.GlobalPosition.X > GlobalPosition.X;
			UpdateFacingDirection(shouldFaceRight);
			
			// Estado de retirada (volando alto)
			if (_isRetreating)
			{
				Vector2 directionToRetreat = (_retreatTarget - GlobalPosition).Normalized();
				Velocity = directionToRetreat * RetreatSpeed;
				anim.Play("fly");
				
				// Si llegó a la altura de retirada
				if (GlobalPosition.Y <= _retreatTarget.Y)
				{
					_isRetreating = false;
					GD.Print("✅ Pájaro listo para atacar de nuevo");
				}
			}
			// Estado de ataque en picada
			else if (_isDiving)
			{
				Vector2 directionToDive = (_diveTarget - GlobalPosition).Normalized();
				Velocity = directionToDive * DiveSpeed;
				AttackBox.Monitoring = true;
				
				// Si llegó cerca del objetivo
				if (GlobalPosition.DistanceTo(_diveTarget) < 20f)
				{
					_isDiving = false;
					AttackBox.Monitoring = false;
				}
			}
			else if (_isPreparingDive)
			{
				// Preparando el ataque - quedarse quieto en el aire
				Velocity = Vector2.Zero;
				anim.Play("idle");
				
				_diveTimer -= (float)delta;
				if (_diveTimer <= 0)
				{
					// Iniciar picada
					_isPreparingDive = false;
					_isDiving = true;
					_isAttacking = true;
					_attackHitPlayer = false;
					_diveTarget = _player.GlobalPosition;
					anim.Play("dive");
					GD.Print("🦅 ¡Pájaro en picada!");
				}
			}
			else if (distanceToPlayer <= AttackRange && _canAttack && !_isRetreating)
			{
				// Iniciar preparación de ataque
				_isPreparingDive = true;
				_diveTimer = DivePrepTime;
				GD.Print("🎯 Pájaro preparando ataque");
			}
			else if (!_isRetreating)
			{
				// Perseguir al jugador
				Vector2 directionToPlayer = (_player.GlobalPosition - GlobalPosition).Normalized();
				Velocity = directionToPlayer * ChaseSpeed;
				
				if (!_isAttacking)
				{
					anim.Play("fly");
				}
			}
		}
		else
		{
			// Patrullaje
			_isAttacking = false;
			_isDiving = false;
			_isPreparingDive = false;
			_isRetreating = false;
			AttackBox.Monitoring = false;
			
			Velocity = GetPatrolPosition((float)delta);
			
			// Actualizar dirección según velocidad
			if (Velocity.X != 0)
			{
				UpdateFacingDirection(Velocity.X > 0);
			}
			
			anim.Play("fly");
		}
		
		MoveAndSlide();
	}
}
