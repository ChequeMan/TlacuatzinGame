using Godot;
using System;
using System.Collections.Generic;

public partial class JaguarBoss : CharacterBody2D
{
	// ========== CONFIGURACIÓN DE NIVEL ==========
	[ExportGroup("Boss Level")]
	[Export] public int BossLevel = 1; // 1, 2, o 3
	
	// ========== ESTADÍSTICAS BASE (SE ESCALAN CON EL NIVEL) ==========
	[ExportGroup("Base Stats")]
	[Export] public int BaseMaxHealth = 20;
	[Export] public float BaseGravity = 980f;
	[Export] public int BaseWalkSpeed = 120;
	[Export] public int BaseChargeSpeed = 450;
	[Export] public float BaseKnockbackForce = 180f;
	
	// ========== RANGOS ==========
	[ExportGroup("Ranges")]
	[Export] public float MeleeAttackRange = 120f;
	[Export] public float AggroRange = 600f;
	
	// ========== FRAMES DE ATAQUE ==========
	[ExportGroup("Attack Frames")]
	[Export] public int ClawAttackFrameStart = 5;
	[Export] public int ClawAttackFrameEnd = 8;
	[Export] public int BiteAttackFrameStart = 6;
	[Export] public int BiteAttackFrameEnd = 10;
	[Export] public int PounceAttackFrameStart = 4;
	[Export] public int PounceAttackFrameEnd = 7;
	
	// ========== COOLDOWNS BASE ==========
	[ExportGroup("Base Cooldowns")]
	[Export] public float BaseClawCooldown = 1.5f;
	[Export] public float BaseBiteCooldown = 2.5f;
	[Export] public float BasePounceCooldown = 4.0f;
	[Export] public float BaseFuryCooldown = 8.0f; // Solo nivel 2+
	[Export] public float BaseShadowDashCooldown = 6.0f; // Solo nivel 3
	
	// ========== COMPONENTES ==========
	private AnimatedSprite2D anim;
	private CollisionShape2D _collisionShape;
	
	// Hitboxes
	private Area2D ClawHitbox;
	private Area2D BiteHitbox;
	private Area2D PounceHitbox;
	private Area2D HurtBox;
	
	// ========== ESTADÍSTICAS ESCALADAS ==========
	private int _maxHealth;
	private int _currentHealth;
	private float _gravity;
	private int _walkSpeed;
	private int _chargeSpeed;
	private float _knockbackForce;
	
	// Cooldowns escalados
	private float _clawCooldown;
	private float _biteCooldown;
	private float _pounceCooldown;
	private float _furyCooldown;
	private float _shadowDashCooldown;
	
	// ========== ESTADO ==========
	private enum JaguarState 
	{ 
		Idle, 
		Stalking,      // Camina lento acechando
		Running,       // Corre hacia el jugador
		ClawAttack, 
		BiteAttack, 
		Pouncing,      // Preparando salto
		PounceAir,     // En el aire saltando
		FuryMode,      // Modo furia (nivel 2+)
		ShadowDash,    // Dash con invisibilidad (nivel 3)
		Hurt, 
		Dead 
	}
	private JaguarState _currentState = JaguarState.Idle;
	
	// Estado del boss
	private bool _isDead = false;
	private bool _isHurt = false;
	private float _hurtTimer = 0f;
	private float _hurtDuration = 0.3f;
	private Vector2 _knockbackVelocity = Vector2.Zero;
	private bool _isKnockedBack = false;
	
	// Comportamiento
	private CharacterBody2D _player;
	private bool _facingRight = true;
	
	// Sistema de ataques
	private Dictionary<string, float> _attackCooldowns = new Dictionary<string, float>();
	
	// Variables de Pounce (salto)
	private Vector2 _pounceVelocity = Vector2.Zero;
	private bool _isPouncing = false;
	
	// Modo furia (nivel 2+)
	private bool _isFuryMode = false;
	private float _furyDuration = 8f;
	private float _furyTimer = 0f;
	
	// Shadow Dash (nivel 3)
	private bool _isShadowDashing = false;
	private float _shadowDashDuration = 0.8f;
	private float _shadowDashTimer = 0f;
	private Vector2 _dashDirection = Vector2.Zero;
	
	// Raycasts para detección
	private RayCast2D _wallDetector;
	private RayCast2D _ledgeDetector;
	
	public override void _Ready()
	{
		// Validar nivel
		BossLevel = Mathf.Clamp(BossLevel, 1, 3);
		
		// Obtener componentes
		anim = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_collisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
		
		// Hitboxes
		ClawHitbox = GetNode<Area2D>("ClawHitbox");
		ClawHitbox.Monitoring = false;
		ClawHitbox.BodyEntered += OnClawHitboxBodyEntered;
		
		BiteHitbox = GetNode<Area2D>("BiteHitbox");
		BiteHitbox.Monitoring = false;
		BiteHitbox.BodyEntered += OnBiteHitboxBodyEntered;
		
		PounceHitbox = GetNode<Area2D>("PounceHitbox");
		PounceHitbox.Monitoring = false;
		PounceHitbox.BodyEntered += OnPounceHitboxBodyEntered;
		
		HurtBox = GetNode<Area2D>("HurtBox");
		HurtBox.BodyEntered += OnHurtBoxBodyEntered;
		
		// Conectar señales
		anim.AnimationFinished += OnAnimationFinished;
		anim.FrameChanged += OnFrameChanged;
		
		AddToGroup("Boss");
		AddToGroup($"JaguarBoss_Level{BossLevel}");
		
		// Escalar estadísticas según nivel
		ScaleStatsForLevel();
		
		// Inicializar cooldowns
		InitializeCooldowns();
		
		// Crear detectores
		CreateDetectors();
		
		GD.Print($"🐆 Jaguar Boss Nivel {BossLevel} inicializado - Vida: {_maxHealth}, Velocidad: {_walkSpeed}/{_chargeSpeed}");
	}
	
	private void ScaleStatsForLevel()
	{
		float multiplier = 1.0f + (BossLevel - 1) * 0.5f; // Nivel 1: 1x, Nivel 2: 1.5x, Nivel 3: 2x
		
		_maxHealth = Mathf.RoundToInt(BaseMaxHealth * multiplier);
		_currentHealth = _maxHealth;
		_gravity = BaseGravity;
		_walkSpeed = Mathf.RoundToInt(BaseWalkSpeed * multiplier);
		_chargeSpeed = Mathf.RoundToInt(BaseChargeSpeed * multiplier);
		_knockbackForce = BaseKnockbackForce * (1.0f - (BossLevel - 1) * 0.2f); // Menos knockback en niveles altos
		
		// Cooldowns más rápidos en niveles altos
		float cooldownMultiplier = 1.0f - (BossLevel - 1) * 0.15f;
		_clawCooldown = BaseClawCooldown * cooldownMultiplier;
		_biteCooldown = BaseBiteCooldown * cooldownMultiplier;
		_pounceCooldown = BasePounceCooldown * cooldownMultiplier;
		_furyCooldown = BaseFuryCooldown * cooldownMultiplier;
		_shadowDashCooldown = BaseShadowDashCooldown * cooldownMultiplier;
	}
	
	private void InitializeCooldowns()
	{
		_attackCooldowns["claw"] = 0f;
		_attackCooldowns["bite"] = 0f;
		_attackCooldowns["pounce"] = 0f;
		_attackCooldowns["fury"] = 0f;
		_attackCooldowns["shadowdash"] = 0f;
	}
	
	private void CreateDetectors()
	{
		_wallDetector = new RayCast2D();
		_wallDetector.Name = "WallDetector";
		_wallDetector.Enabled = true;
		_wallDetector.TargetPosition = new Vector2(40, 0);
		_wallDetector.CollideWithAreas = false;
		_wallDetector.CollideWithBodies = true;
		AddChild(_wallDetector);
		
		_ledgeDetector = new RayCast2D();
		_ledgeDetector.Name = "LedgeDetector";
		_ledgeDetector.Enabled = true;
		_ledgeDetector.Position = new Vector2(30, 0);
		_ledgeDetector.TargetPosition = new Vector2(0, 40);
		_ledgeDetector.CollideWithAreas = false;
		_ledgeDetector.CollideWithBodies = true;
		AddChild(_ledgeDetector);
	}
	
	// ========== DETECCIÓN DE DAÑO ==========
	private void OnHurtBoxBodyEntered(Node2D body)
	{
		if (body.IsInGroup("Player") && !_isDead && !_isHurt && !_isShadowDashing)
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
		if (_isDead || _isShadowDashing) return; // Invulnerable durante shadow dash
		
		// Reducción de daño en fury mode
		if (_isFuryMode && BossLevel >= 2)
		{
			damage = Mathf.Max(1, damage / 2);
			GD.Print("🔥 Daño reducido por Fury Mode!");
		}
		
		_currentHealth -= damage;
		GD.Print($"🐆 Jaguar Nivel {BossLevel} golpeado! Vida: {_currentHealth}/{_maxHealth}");
		
		// Knockback (reducido en niveles altos)
		_knockbackVelocity = knockbackDirection * _knockbackForce;
		_isKnockedBack = true;
		
		// Cancelar ataques
		CancelAllAttacks();
		
		// Activar fury mode si vida baja (nivel 2+)
		if (BossLevel >= 2 && !_isFuryMode && _currentHealth <= _maxHealth * 0.4f && _attackCooldowns["fury"] <= 0)
		{
			ActivateFuryMode();
			return; // No hacer hurt animation, pasar directo a fury
		}
		
		if (_currentHealth <= 0)
		{
			Die();
		}
		else
		{
			_isHurt = true;
			_hurtTimer = _hurtDuration;
			ChangeState(JaguarState.Hurt);
			anim.Play("JaguarHurt");
			FlashDamage();
		}
	}
	
	private async void FlashDamage()
	{
		Modulate = new Color(1, 0.3f, 0.3f);
		await ToSignal(GetTree().CreateTimer(0.1), "timeout");
		Modulate = Colors.White;
	}
	
	private void ActivateFuryMode()
	{
		_isFuryMode = true;
		_furyTimer = _furyDuration;
		_attackCooldowns["fury"] = _furyCooldown;
		ChangeState(JaguarState.FuryMode);
		anim.Play("JaguarFuryStart");
		Modulate = new Color(1, 0.5f, 0.2f); // Tinte naranja
		GD.Print("🔥 ¡FURY MODE ACTIVADO!");
	}
	
	private void Die()
	{
		if (_isDead) return;
		
		_isDead = true;
		CancelAllAttacks();
		
		SetCollisionLayerValue(1, false);
		SetCollisionMaskValue(1, false);
		
		if (_collisionShape != null)
			_collisionShape.Disabled = true;
		
		HurtBox.SetDeferred("monitoring", false);
		HurtBox.SetDeferred("monitorable", false);
		
		ChangeState(JaguarState.Dead);
		anim.Play("JaguarDeath");
		Velocity = Vector2.Zero;
		
		GD.Print($"💀 ¡Jaguar Nivel {BossLevel} derrotado!");
	}
	
	private void CancelAllAttacks()
	{
		ClawHitbox.Monitoring = false;
		BiteHitbox.Monitoring = false;
		PounceHitbox.Monitoring = false;
		_isPouncing = false;
		_isShadowDashing = false;
	}
	
	// ========== HITBOX CALLBACKS ==========
	private void OnClawHitboxBodyEntered(Node2D body)
	{
		if (body.IsInGroup("Player"))
		{
			Player player = body as Player;
			if (player != null)
				player.Die();
		}
	}
	
	private void OnBiteHitboxBodyEntered(Node2D body)
	{
		if (body.IsInGroup("Player"))
		{
			Player player = body as Player;
			if (player != null)
				player.Die();
		}
	}
	
	private void OnPounceHitboxBodyEntered(Node2D body)
	{
		if (body.IsInGroup("Player"))
		{
			Player player = body as Player;
			if (player != null)
				player.Die();
		}
	}
	
	// ========== ANIMACIÓN CALLBACKS ==========
	private void OnAnimationFinished()
	{
		switch (anim.Animation)
		{
			case "JaguarClawAttack":
				ClawHitbox.Monitoring = false;
				_attackCooldowns["claw"] = _clawCooldown;
				ChangeState(JaguarState.Idle);
				break;
				
			case "JaguarBiteAttack":
				BiteHitbox.Monitoring = false;
				_attackCooldowns["bite"] = _biteCooldown;
				ChangeState(JaguarState.Idle);
				break;
				
			case "JaguarPouncePrep":
				// Ejecutar salto
				ExecutePounce();
				break;
				
			case "JaguarPounceAir":
				PounceHitbox.Monitoring = false;
				_isPouncing = false;
				_attackCooldowns["pounce"] = _pounceCooldown;
				ChangeState(JaguarState.Idle);
				break;
				
			case "JaguarFuryStart":
				// Continuar en fury mode pero permitir movimiento
				anim.Play("JaguarFuryLoop");
				break;
				
			case "JaguarHurt":
				_isHurt = false;
				_isKnockedBack = false;
				ChangeState(JaguarState.Idle);
				break;
				
			case "JaguarDeath":
				QueueFree();
				break;
		}
	}
	
	private void OnFrameChanged()
	{
		string currentAnim = anim.Animation;
		int frame = anim.Frame;
		
		if (currentAnim == "JaguarClawAttack")
		{
			if (frame == ClawAttackFrameStart)
				ClawHitbox.Monitoring = true;
			else if (frame == ClawAttackFrameEnd)
				ClawHitbox.Monitoring = false;
		}
		else if (currentAnim == "JaguarBiteAttack")
		{
			if (frame == BiteAttackFrameStart)
				BiteHitbox.Monitoring = true;
			else if (frame == BiteAttackFrameEnd)
				BiteHitbox.Monitoring = false;
		}
		else if (currentAnim == "JaguarPounceAir")
		{
			if (frame == PounceAttackFrameStart)
				PounceHitbox.Monitoring = true;
			else if (frame == PounceAttackFrameEnd)
				PounceHitbox.Monitoring = false;
		}
	}
	
	// ========== LÓGICA DE POUNCE ==========
	private void ExecutePounce()
	{
		if (_player == null) return;
		
		Vector2 directionToPlayer = (_player.GlobalPosition - GlobalPosition).Normalized();
		_pounceVelocity = directionToPlayer * _chargeSpeed;
		_pounceVelocity.Y = -400; // Componente vertical
		
		_isPouncing = true;
		PounceHitbox.Monitoring = true;
		ChangeState(JaguarState.PounceAir);
		anim.Play("JaguarPounceAir");
	}
	
	// ========== ACTUALIZACIÓN DE DIRECCIÓN ==========
	private void UpdateFacingDirection(bool shouldFaceRight)
	{
		_facingRight = shouldFaceRight;
		anim.FlipH = _facingRight;
		
		float scaleX = _facingRight ? -1 : 1;
		ClawHitbox.Scale = new Vector2(scaleX, 1);
		BiteHitbox.Scale = new Vector2(scaleX, 1);
		PounceHitbox.Scale = new Vector2(scaleX, 1);
		HurtBox.Scale = new Vector2(scaleX, 1);
		
		// Actualizar raycasts
		if (_wallDetector != null && _ledgeDetector != null)
		{
			float direction = _facingRight ? 1 : -1;
			_wallDetector.TargetPosition = new Vector2(40 * direction, 0);
			_ledgeDetector.Position = new Vector2(30 * direction, 0);
		}
	}
	
	private void ChangeState(JaguarState newState)
	{
		if (_currentState == newState) return;
		_currentState = newState;
	}
	
	// ========== DECISIÓN DE ATAQUES ==========
	private void DecideAttack(float distanceToPlayer)
	{
		List<string> availableAttacks = new List<string>();
		
		// Ataques básicos disponibles en todos los niveles
		if (distanceToPlayer <= MeleeAttackRange * 1.2f)
		{
			if (_attackCooldowns["claw"] <= 0)
				availableAttacks.Add("claw");
			
			if (_attackCooldowns["bite"] <= 0)
				availableAttacks.Add("bite");
		}
		
		// Pounce (salto) - disponible a media distancia
		if (distanceToPlayer > MeleeAttackRange && distanceToPlayer <= 400f)
		{
			if (_attackCooldowns["pounce"] <= 0)
				availableAttacks.Add("pounce");
		}
		
		// Shadow Dash - solo nivel 3
		if (BossLevel >= 3 && distanceToPlayer > 150f && distanceToPlayer <= 500f)
		{
			if (_attackCooldowns["shadowdash"] <= 0)
				availableAttacks.Add("shadowdash");
		}
		
		if (availableAttacks.Count == 0)
		{
			ChangeState(JaguarState.Running);
			return;
		}
		
		// Elegir ataque
		string chosenAttack = availableAttacks[(int)(GD.Randi() % availableAttacks.Count)];
		
		switch (chosenAttack)
		{
			case "claw":
				ChangeState(JaguarState.ClawAttack);
				anim.Play("JaguarClawAttack");
				break;
			case "bite":
				ChangeState(JaguarState.BiteAttack);
				anim.Play("JaguarBiteAttack");
				break;
			case "pounce":
				ChangeState(JaguarState.Pouncing);
				anim.Play("JaguarPouncePrep");
				break;
			case "shadowdash":
				ExecuteShadowDash();
				break;
		}
	}
	
	private void ExecuteShadowDash()
	{
		if (_player == null) return;
		
		_isShadowDashing = true;
		_shadowDashTimer = _shadowDashDuration;
		_dashDirection = (_player.GlobalPosition - GlobalPosition).Normalized();
		_attackCooldowns["shadowdash"] = _shadowDashCooldown;
		
		ChangeState(JaguarState.ShadowDash);
		anim.Play("JaguarShadowDash");
		Modulate = new Color(1, 1, 1, 0.3f); // Semi-transparente
		
		GD.Print("👻 Shadow Dash activado!");
	}
	
	// ========== PHYSICS PROCESS ==========
	public override void _PhysicsProcess(double delta)
	{
		if (_isDead) return;
		
		float d = (float)delta;
		
		// Actualizar cooldowns
		foreach (var key in new List<string>(_attackCooldowns.Keys))
		{
			if (_attackCooldowns[key] > 0)
				_attackCooldowns[key] -= d;
		}
		
		// Actualizar fury mode
		if (_isFuryMode)
		{
			_furyTimer -= d;
			if (_furyTimer <= 0)
			{
				_isFuryMode = false;
				Modulate = Colors.White;
				GD.Print("🔥 Fury Mode terminado");
			}
		}
		
		// Actualizar shadow dash
		if (_isShadowDashing)
		{
			_shadowDashTimer -= d;
			if (_shadowDashTimer <= 0)
			{
				_isShadowDashing = false;
				Modulate = Colors.White;
				ChangeState(JaguarState.Idle);
			}
			else
			{
				// Movimiento rápido durante dash
				Velocity = _dashDirection * _chargeSpeed * 1.5f;
				MoveAndSlide();
				return;
			}
		}
		
		// Gravedad
		if (!IsOnFloor() && !_isPouncing)
		{
			Velocity = new Vector2(Velocity.X, Velocity.Y + _gravity * d);
		}
		
		// Estado de herido
		if (_isHurt)
		{
			_hurtTimer -= d;
			if (_hurtTimer <= 0)
			{
				_isHurt = false;
				_isKnockedBack = false;
				ChangeState(JaguarState.Idle);
			}
			
			if (_isKnockedBack)
			{
				Velocity = new Vector2(_knockbackVelocity.X, Velocity.Y);
				_knockbackVelocity = _knockbackVelocity.Lerp(Vector2.Zero, 0.1f);
			}
			
			MoveAndSlide();
			return;
		}
		
		// Buscar jugador
		if (_player == null)
		{
			_player = GetTree().GetFirstNodeInGroup("Player") as CharacterBody2D;
		}
		
		if (_player != null)
		{
			float distanceToPlayer = GlobalPosition.DistanceTo(_player.GlobalPosition);
			bool shouldFaceRight = _player.GlobalPosition.X > GlobalPosition.X;
			UpdateFacingDirection(shouldFaceRight);
			
			// Máquina de estados
			switch (_currentState)
			{
				case JaguarState.Idle:
					Velocity = new Vector2(0, Velocity.Y);
					anim.Play("JaguarIdle");
					
					if (distanceToPlayer <= AggroRange)
					{
						// Cambiar a stalking o running según distancia
						if (distanceToPlayer > 250f)
							ChangeState(JaguarState.Stalking);
						else
							DecideAttack(distanceToPlayer);
					}
					break;
					
				case JaguarState.Stalking:
					// Movimiento sigiloso lento
					int stalkSpeed = _walkSpeed / 2;
					if (shouldFaceRight)
						Velocity = new Vector2(stalkSpeed, Velocity.Y);
					else
						Velocity = new Vector2(-stalkSpeed, Velocity.Y);
					
					anim.Play("JaguarStalking");
					
					if (distanceToPlayer <= 250f)
					{
						ChangeState(JaguarState.Running);
					}
					break;
					
				case JaguarState.Running:
					// Correr hacia el jugador
					int runSpeed = _isFuryMode ? (int)(_chargeSpeed * 0.8f) : _walkSpeed;
					if (shouldFaceRight)
						Velocity = new Vector2(runSpeed, Velocity.Y);
					else
						Velocity = new Vector2(-runSpeed, Velocity.Y);
					
					anim.Play("JaguarRun");
					
					if (distanceToPlayer <= MeleeAttackRange * 1.5f)
					{
						DecideAttack(distanceToPlayer);
					}
					break;
					
				case JaguarState.PounceAir:
					// Movimiento durante pounce
					if (_isPouncing)
					{
						Velocity = _pounceVelocity;
						_pounceVelocity.Y += _gravity * d * 0.6f; // Gravedad reducida
						
						if (IsOnFloor())
						{
							_isPouncing = false;
							PounceHitbox.Monitoring = false;
							ChangeState(JaguarState.Idle);
						}
					}
					break;
					
				case JaguarState.FuryMode:
					// En fury mode loop, permitir movimiento agresivo
					if (anim.Animation == "JaguarFuryLoop")
					{
						int furySpeed = (int)(_chargeSpeed * 0.8f);
						if (shouldFaceRight)
							Velocity = new Vector2(furySpeed, Velocity.Y);
						else
							Velocity = new Vector2(-furySpeed, Velocity.Y);
						
						if (distanceToPlayer <= MeleeAttackRange * 1.2f)
						{
							DecideAttack(distanceToPlayer);
						}
					}
					else
					{
						Velocity = new Vector2(0, Velocity.Y);
					}
					break;
					
				case JaguarState.ClawAttack:
				case JaguarState.BiteAttack:
				case JaguarState.Pouncing:
					Velocity = new Vector2(0, Velocity.Y);
					break;
			}
		}
		else
		{
			Velocity = new Vector2(0, Velocity.Y);
			anim.Play("JaguarIdle");
		}
		
		MoveAndSlide();
	}
}
