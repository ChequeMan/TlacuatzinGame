using Godot;
using System;

public partial class Enemy : CharacterBody2D
{
	[Export] public int Speed = 100;
	[Export] public int ChaseSpeed = 300;
	[Export] public int PatrolDistance = 200;
	[Export] public float Gravity = 980f;
	[Export] public float AttackRange = 50f;
	[Export] public float AggroRange = 300f;
	[Export] public int MaxHealth = 3;
	[Export] public float KnockbackForce = 200f;
	[Export] public float StunDuration = 0.3f;
	
	// ✅ NUEVO: Daño que hace al jugador
	[Export] public int AttackDamage = 1;
	
	private Area2D AttackBox;
	[Export] public int AttackFrameStart = 9;
	[Export] public int AttackFrameEnds = 13;
	[Export] public float AttackCooldown = 1.5f;
	
	private Vector2 _startPosition;
	private bool _movingRight = true;
	private bool _isAttacking = false;
	private bool _isChasing = false;
	private CharacterBody2D _player;
	
	private AnimatedSprite2D anim;
	private Area2D AlertBox;
	private Area2D HurtBox;
	
	// Sistema de vida mejorado
	private int _currentHealth;
	private bool _isDead = false;
	private bool _isHurt = false;
	private float _hurtTimer = 0f;
	private float _hurtDuration = 0.5f;
	private CollisionShape2D _collisionShape;
	
	// Nuevos sistemas
	private float _attackCooldownTimer = 0f;
	private bool _canAttack = true;
	private Vector2 _knockbackVelocity = Vector2.Zero;
	private bool _isKnockedBack = false;
	private float _detectionTimer = 0f;
	private bool _playerSpotted = false;
	
	// Sistema de anticipación de ataque
	private bool _isPreparingAttack = false;
	private float _attackPreparationTime = 0.3f;
	private float _attackPreparationTimer = 0f;
	
	// Sistema de patrullaje inteligente
	private float _idleTimer = 0f;
	private float _idleDuration = 2f;
	private bool _isIdling = false;
	private float _patrolPauseChance = 0.3f;
	
	private bool _facingRight = true;
	
	// Detección de obstáculos
	private RayCast2D _wallDetector;
	private RayCast2D _ledgeDetector;
	
	public override void _Ready()
	{
		anim = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_startPosition = GlobalPosition;
		_currentHealth = MaxHealth;
		
		AlertBox = GetNode<Area2D>("AlertBox");
		AlertBox.BodyEntered += OnBodyEntered;
		AlertBox.BodyExited += OnBodyExited;
		
		anim.AnimationFinished += OnAnimationFinished;
		
		AttackBox = GetNode<Area2D>("Attackbox");
		AttackBox.Monitoring = false;
		AttackBox.BodyEntered += OnAttackBoxBodyEntered;
		anim.FrameChanged += OnFrameChanged;
		
		HurtBox = GetNode<Area2D>("Hurtbox");
		HurtBox.BodyEntered += OnHurtBoxBodyEntered;
		
		_collisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
		
		CreateObstacleDetectors();
	}
	
	private void CreateObstacleDetectors()
	{
		// Raycast para detectar paredes
		_wallDetector = new RayCast2D();
		_wallDetector.Name = "WallDetector";
		_wallDetector.Enabled = true;
		_wallDetector.TargetPosition = new Vector2(30, 0);
		_wallDetector.CollideWithAreas = false;
		_wallDetector.CollideWithBodies = true;
		AddChild(_wallDetector);
		
		// Raycast para detectar precipicios
		_ledgeDetector = new RayCast2D();
		_ledgeDetector.Name = "LedgeDetector";
		_ledgeDetector.Enabled = true;
		_ledgeDetector.Position = new Vector2(20, 0);
		_ledgeDetector.TargetPosition = new Vector2(0, 30);
		_ledgeDetector.CollideWithAreas = false;
		_ledgeDetector.CollideWithBodies = true;
		AddChild(_ledgeDetector);
	}
	
	private void UpdateRaycastDirections()
	{
		if (_wallDetector != null && _ledgeDetector != null)
		{
			float direction = _facingRight ? 1 : -1;
			_wallDetector.TargetPosition = new Vector2(30 * direction, 0);
			_ledgeDetector.Position = new Vector2(20 * direction, 0);
		}
	}
	
	private bool IsObstacleAhead()
	{
		if (_wallDetector == null || _ledgeDetector == null) return false;
		
		bool wallAhead = _wallDetector.IsColliding();
		bool ledgeAhead = !_ledgeDetector.IsColliding() && IsOnFloor();
		
		if (wallAhead && _wallDetector.GetCollider() is Node2D collider)
		{
			if (collider.IsInGroup("Player"))
			{
				return false;
			}
		}
		
		return wallAhead || ledgeAhead;
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
		GD.Print($"Enemigo golpeado! Vida restante: {_currentHealth}/{MaxHealth}");
		
		_knockbackVelocity = knockbackDirection * KnockbackForce;
		_isKnockedBack = true;
		
		_isAttacking = false;
		_isPreparingAttack = false;
		_attackPreparationTimer = 0f;
		AttackBox.Monitoring = false;
		
		_canAttack = false;
		_attackCooldownTimer = AttackCooldown * 0.5f;
		
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
			anim.Play("InjuredEscuincle");
			
			FlashDamage();
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
		_isPreparingAttack = false;
		AttackBox.Monitoring = false;
		AlertBox.Monitoring = false;
		HurtBox.Monitoring = false;
		
		SetCollisionLayerValue(1, false); 
		SetCollisionMaskValue(1, false);
		
		if (_collisionShape != null)
		{
			_collisionShape.Disabled = true;
		}
		
		HurtBox.Monitoring = false;
		HurtBox.Monitorable = false;
		
		anim.Play("EscuincleDies");
		Velocity = Vector2.Zero;
		GD.Print("¡Enemigo eliminado!");
	}
	
	// ✅ MODIFICADO: Usar TakeDamage en lugar de Die directo
	private void OnAttackBoxBodyEntered(Node2D body)
	{
		if (body.IsInGroup("Player") && !_isDead)
		{
			Player player = body as Player;
			if (player != null)
			{
				GD.Print($"💥 Enemigo golpeó al jugador causando {AttackDamage} de daño");
				player.TakeDamage(AttackDamage);
			}
		}
	}
	
	private void OnBodyEntered(Node2D body)
	{
		if(body.IsInGroup("Player") && !_isDead)
		{
			_playerSpotted = true;
			_isChasing = true;
			_player = body as CharacterBody2D;
			_detectionTimer = 0f;
		}
	}
	
	private void OnBodyExited(Node2D body)
	{
		if(body.IsInGroup("Player"))
		{
			_detectionTimer = 2f;
		}
	}
	
	private void OnAnimationFinished()
	{
		if (anim.Animation == "attack")
		{
			_isAttacking = false;
			_attackCooldownTimer = AttackCooldown;
			_canAttack = false;
			AttackBox.Monitoring = false;
		}
		else if (anim.Animation == "InjuredEscuincle")
		{
			_isHurt = false;
			_isKnockedBack = false;
		}
		else if (anim.Animation == "EscuincleDies")
		{
			QueueFree();
		}
	}
	
	private void OnFrameChanged()
	{
		if(anim.Animation == "attack")
		{
			int currentFrame = anim.Frame;
			
			if(currentFrame == AttackFrameStart)
			{
				AttackBox.Monitoring = true;
			}
			else if (currentFrame == AttackFrameEnds)
			{
				AttackBox.Monitoring = false;
			}
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
	
	public override void _PhysicsProcess(double delta)
	{
		if (_isDead) return;
		
		if (!_canAttack)
		{
			_attackCooldownTimer -= (float)delta;
			if (_attackCooldownTimer <= 0)
			{
				_canAttack = true;
				GD.Print("✅ Enemigo puede atacar de nuevo");
			}
		}
		
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
		
		if (_isHurt)
		{
			_hurtTimer -= (float)delta;
			if (_hurtTimer <= 0)
			{
				_isHurt = false;
				_isKnockedBack = false;
			}
			
			if (_isKnockedBack)
			{
				Velocity = new Vector2(_knockbackVelocity.X, Velocity.Y);
				_knockbackVelocity = _knockbackVelocity.Lerp(Vector2.Zero, 0.1f);
			}
			
			if (!IsOnFloor())
			{
				Velocity = new Vector2(Velocity.X, Velocity.Y + Gravity * (float)delta);
			}
			else if (!_isKnockedBack)
			{
				Velocity = new Vector2(0, Velocity.Y);
			}
			
			MoveAndSlide();
			return;
		}
		
		if (!IsOnFloor())
		{
			Velocity = new Vector2(Velocity.X, Velocity.Y + Gravity * (float)delta);
		}
		
		if (_isChasing && _player != null)
		{
			float distanceToPlayer = GlobalPosition.DistanceTo(_player.GlobalPosition);
			
			if (Time.GetTicksMsec() % 500 < 16)
			{
				GD.Print($"Distancia: {distanceToPlayer:F1} | AttackRange: {AttackRange} | CanAttack: {_canAttack} | Attacking: {_isAttacking} | Preparing: {_isPreparingAttack}");
			}
			
			bool shouldFaceRight = _player.GlobalPosition.X > GlobalPosition.X;
			UpdateFacingDirection(shouldFaceRight);
			
			if (_isAttacking)
			{
				Velocity = new Vector2(0, Velocity.Y);
			}
			else if (_isPreparingAttack)
			{
				if (distanceToPlayer > AttackRange * 2f)
				{
					GD.Print("❌ Jugador se alejó, cancelando ataque");
					_isPreparingAttack = false;
					_attackPreparationTimer = 0;
				}
				else
				{
					_attackPreparationTimer -= (float)delta;
					if (_attackPreparationTimer <= 0)
					{
						_isPreparingAttack = false;
						_isAttacking = true;
						GD.Print("⚔️ ATACANDO!");
						anim.Play("attack");
					}
					Velocity = new Vector2(0, Velocity.Y);
					return;
				}
			}
			
			if (distanceToPlayer <= AttackRange * 1.2f && _canAttack && !_isPreparingAttack && !_isAttacking)
			{
				GD.Print($"🎯 Preparando ataque! Distancia: {distanceToPlayer:F1}");
				_isPreparingAttack = true;
				_attackPreparationTimer = _attackPreparationTime;
				Velocity = new Vector2(0, Velocity.Y);
				anim.Play("idle");
			}
			else if (distanceToPlayer > AttackRange * 1.2f)
			{
				int currentSpeed = _playerSpotted ? ChaseSpeed : Speed;
				
				if (shouldFaceRight)
				{
					Velocity = new Vector2(currentSpeed, Velocity.Y);
				}
				else
				{
					Velocity = new Vector2(-currentSpeed, Velocity.Y);
				}
				
				if (!_isPreparingAttack && !_isAttacking)
				{
					anim.Play("walk");
				}
			}
			else
			{
				Velocity = new Vector2(0, Velocity.Y);
				if (!_isPreparingAttack && !_isAttacking)
				{
					anim.Play("idle");
				}
			}
		}
		else
		{
			_isAttacking = false;
			_isPreparingAttack = false;
			
			if (_isIdling)
			{
				_idleTimer -= (float)delta;
				if (_idleTimer <= 0)
				{
					_isIdling = false;
				}
				
				Velocity = new Vector2(0, Velocity.Y);
				anim.Play("idle");
			}
			else
			{
				UpdateFacingDirection(_movingRight);
				
				if (_movingRight)
				{
					Velocity = new Vector2(Speed, Velocity.Y);
					anim.Play("walk");
				}
				else
				{
					Velocity = new Vector2(-Speed, Velocity.Y);
					anim.Play("walk");
				}
				
				if (_movingRight && GlobalPosition.X > _startPosition.X + PatrolDistance)
				{
					_movingRight = false;
					if (GD.Randf() < _patrolPauseChance)
					{
						_isIdling = true;
						_idleTimer = _idleDuration;
					}
				}
				else if (!_movingRight && GlobalPosition.X < _startPosition.X - PatrolDistance)
				{
					_movingRight = true;
					if (GD.Randf() < _patrolPauseChance)
					{
						_isIdling = true;
						_idleTimer = _idleDuration;
					}
				}
			}
		}
		
		MoveAndSlide();
	}
}
