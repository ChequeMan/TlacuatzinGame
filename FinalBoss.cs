using Godot;
using System;
using System.Collections.Generic;

public partial class FinalBoss : CharacterBody2D
{
	// ========== ESTADÍSTICAS BÁSICAS ==========
	[Export] public int MaxHealth = 50;
	[Export] public float Gravity = 980f;
	[Export] public int WalkSpeed = 80;
	[Export] public int ChargeSpeed = 400;
	[Export] public float KnockbackForce = 150f;
	
	// ========== RANGOS Y DISTANCIAS ==========
	[Export] public float MeleeAttackRange = 100f;
	[Export] public float RangedAttackRange = 400f;
	[Export] public float AggroRange = 500f;
	
	// ========== CONFIGURACIÓN DE ATAQUES ==========
	[ExportGroup("Melee Attack")]
	[Export] public int MeleeAttackFrameStart = 8;
	[Export] public int MeleeAttackFrameEnd = 12;
	[Export] public int MeleeDamage = 2;
	
	[ExportGroup("Heavy Attack")]
	[Export] public int HeavyAttackFrameStart = 15;
	[Export] public int HeavyAttackFrameEnd = 20;
	[Export] public int HeavyDamage = 3;
	
	[ExportGroup("Charge Attack")]
	[Export] public float ChargePreparationTime = 1.0f;
	[Export] public float ChargeDuration = 1.5f;
	[Export] public int ChargeDamage = 4;
	
	[ExportGroup("Projectile Attack")]
	[Export] public PackedScene ProjectileScene;
	[Export] public float ProjectileSpeed = 300f;
	[Export] public int ProjectileDamage = 1;
	
	[ExportGroup("Special Attack")]
	[Export] public float GroundSlamRadius = 200f;
	[Export] public int GroundSlamDamage = 3;
	
	// ========== COOLDOWNS ==========
	[Export] public float MeleeAttackCooldown = 2.0f;
	[Export] public float HeavyAttackCooldown = 4.0f;
	[Export] public float ChargeAttackCooldown = 6.0f;
	[Export] public float ProjectileAttackCooldown = 3.0f;
	[Export] public float GroundSlamCooldown = 8.0f;
	
	// ✅ SEÑALES PARA LA UI
	[Signal]
	public delegate void HealthChangedEventHandler(int currentHealth, int maxHealth);
	
	[Signal]
	public delegate void BossDefeatedEventHandler();
	
	// ========== COMPONENTES ==========
	private AnimatedSprite2D anim;
	private Area2D MeleeHitbox;
	private Area2D HeavyHitbox;
	private Area2D ChargeHitbox;
	private Area2D HurtBox;
	private CollisionShape2D _collisionShape;
	private Marker2D ProjectileSpawnPoint;
	
	// 🔊 Audio Players - SEPARADOS PARA MÚSICA Y EFECTOS
	private AudioStreamPlayer2D SoundWalk;
	private AudioStreamPlayer2D SoundMeleeAttack;
	private AudioStreamPlayer2D SoundHeavyAttack;
	private AudioStreamPlayer2D SoundChargePrep;
	private AudioStreamPlayer2D SoundCharging;
	private AudioStreamPlayer2D SoundProjectile;
	private AudioStreamPlayer2D SoundGroundSlam;
	private AudioStreamPlayer2D SoundHurt;
	private AudioStreamPlayer2D SoundDeath;
	private AudioStreamPlayer2D SoundPhaseChange;
	
	// 🎵 Música en canal SEPARADO (AudioStreamPlayer sin posición)
	private AudioStreamPlayer BossMusic;
	
	// 🎵 Control de música
	private bool _musicStarted = false;
	
	// ========== ESTADO DEL BOSS ==========
	private int _currentHealth;
	private bool _isDead = false;
	private bool _isHurt = false;
	private float _hurtTimer = 0f;
	private float _hurtDuration = 0.4f;
	private Vector2 _knockbackVelocity = Vector2.Zero;
	private bool _isKnockedBack = false;
	
	// ========== COMPORTAMIENTO ==========
	private CharacterBody2D _player;
	private bool _isChasing = false;
	private bool _facingRight = true;
	
	// ========== SISTEMA DE ATAQUES ==========
	private enum BossState 
	{ 
		Idle, 
		Walking, 
		MeleeAttack, 
		HeavyAttack, 
		ChargePrep, 
		Charging, 
		ProjectileAttack,
		GroundSlam,
		Hurt, 
		Dead 
	}
	private BossState _currentState = BossState.Idle;
	
	// Cooldowns de ataques
	private Dictionary<string, float> _attackCooldowns = new Dictionary<string, float>();
	
	// Variables de Charge Attack
	private float _chargeTimer = 0f;
	private Vector2 _chargeDirection = Vector2.Zero;
	
	// Fases del boss (cambia comportamiento según vida)
	private int _currentPhase = 1; // 1, 2, 3
	
	// ========== INICIALIZACIÓN ==========
	public override void _Ready()
	{
		anim = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_collisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
		
		// Hitboxes de ataques
		MeleeHitbox = GetNode<Area2D>("MeleeHitbox");
		MeleeHitbox.Monitoring = false;
		MeleeHitbox.BodyEntered += OnMeleeHitboxBodyEntered;
		
		HeavyHitbox = GetNode<Area2D>("HeavyHitbox");
		HeavyHitbox.Monitoring = false;
		HeavyHitbox.BodyEntered += OnHeavyHitboxBodyEntered;
		
		ChargeHitbox = GetNode<Area2D>("ChargeHitbox");
		ChargeHitbox.Monitoring = false;
		ChargeHitbox.BodyEntered += OnChargeHitboxBodyEntered;
		
		HurtBox = GetNode<Area2D>("HurtBox");
		HurtBox.BodyEntered += OnHurtBoxBodyEntered;
		
		ProjectileSpawnPoint = GetNode<Marker2D>("ProjectileSpawnPoint");
		
		// 🔊 Obtener referencias a los AudioStreamPlayer hijos
		SoundWalk = GetNodeOrNull<AudioStreamPlayer2D>("SoundWalk");
		SoundMeleeAttack = GetNodeOrNull<AudioStreamPlayer2D>("SoundMeleeAttack");
		SoundHeavyAttack = GetNodeOrNull<AudioStreamPlayer2D>("SoundHeavyAttack");
		SoundChargePrep = GetNodeOrNull<AudioStreamPlayer2D>("SoundChargePrep");
		SoundCharging = GetNodeOrNull<AudioStreamPlayer2D>("SoundCharging");
		SoundProjectile = GetNodeOrNull<AudioStreamPlayer2D>("SoundProjectile");
		SoundGroundSlam = GetNodeOrNull<AudioStreamPlayer2D>("SoundGroundSlam");
		SoundHurt = GetNodeOrNull<AudioStreamPlayer2D>("SoundHurt");
		SoundDeath = GetNodeOrNull<AudioStreamPlayer2D>("SoundDeath");
		SoundPhaseChange = GetNodeOrNull<AudioStreamPlayer2D>("SoundPhaseChange");
		
		// 🎵 Música en canal separado
		BossMusic = GetNodeOrNull<AudioStreamPlayer>("BossMusic");
		
		// Inicializar
		_currentHealth = MaxHealth;
		anim.AnimationFinished += OnAnimationFinished;
		anim.FrameChanged += OnFrameChanged;
		
		AddToGroup("Boss");
		
		// Inicializar cooldowns
		_attackCooldowns["melee"] = 0f;
		_attackCooldowns["heavy"] = 0f;
		_attackCooldowns["charge"] = 0f;
		_attackCooldowns["projectile"] = 0f;
		_attackCooldowns["groundslam"] = 0f;
		
		// ✅ Emitir señal inicial de vida
		EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);
	}
	
	// 🔊 MÉTODO PARA REPRODUCIR SONIDOS (SOLO EFECTOS, NO MÚSICA)
	private void PlaySound(AudioStreamPlayer2D sound)
	{
		if (sound != null && sound.Stream != null)
		{
			sound.Play();
		}
	}
	
	// ✅ MÉTODO PÚBLICO PARA OBTENER VIDA
	public int GetCurrentHealth()
	{
		return _currentHealth;
	}
	
	// ========== DETECCIÓN DE GOLPES ==========
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
		if (_isDead) return;
		
		// Reducción de daño en fase 3
		if (_currentPhase == 3)
		{
			damage = Mathf.Max(1, damage / 2);
		}
		
		_currentHealth -= damage;
		GD.Print($"Boss golpeado! Vida: {_currentHealth}/{MaxHealth}");
		
		// ✅ Emitir señal de cambio de vida
		EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);
		
		// 🔊 Sonido de daño
		PlaySound(SoundHurt);
		
		// Knockback
		_knockbackVelocity = knockbackDirection * KnockbackForce;
		_isKnockedBack = true;
		
		// Cancelar ataques
		CancelAllAttacks();
		
		// Cambiar fase según vida
		UpdatePhase();
		
		if (_currentHealth <= 0)
		{
			Die();
		}
		else
		{
			ChangeState(BossState.Hurt);
			_isHurt = true;
			_hurtTimer = _hurtDuration;
			anim.Play("BossHurt");
			FlashDamage();
		}
	}
	
	private async void FlashDamage()
	{
		Modulate = new Color(1, 0.2f, 0.2f);
		await ToSignal(GetTree().CreateTimer(0.1), "timeout");
		Modulate = Colors.White;
	}
	
	private void UpdatePhase()
	{
		float healthPercent = (float)_currentHealth / MaxHealth;
		
		if (healthPercent <= 0.33f && _currentPhase < 3)
		{
			_currentPhase = 3;
			GD.Print("⚡ FASE 3: BOSS FURIOSO!");
			
			// 🔊 Sonido de cambio de fase
			PlaySound(SoundPhaseChange);
			
			// Reducir todos los cooldowns a la mitad
			MeleeAttackCooldown *= 0.5f;
			HeavyAttackCooldown *= 0.5f;
			ChargeAttackCooldown *= 0.5f;
			ProjectileAttackCooldown *= 0.5f;
		}
		else if (healthPercent <= 0.66f && _currentPhase < 2)
		{
			_currentPhase = 2;
			GD.Print("🔥 FASE 2: BOSS ENOJADO!");
			
			// 🔊 Sonido de cambio de fase
			PlaySound(SoundPhaseChange);
			
			// Aumentar velocidades
			WalkSpeed = (int)(WalkSpeed * 1.3f);
			ChargeSpeed = (int)(ChargeSpeed * 1.2f);
		}
	}
	
	private void Die()
	{
		if (_isDead) return;
		
		_isDead = true;
		ChangeState(BossState.Dead);
		CancelAllAttacks();
		
		// 🔊 Sonido de muerte
		PlaySound(SoundDeath);
		
		// 🎵 Detener música
		if (BossMusic != null && BossMusic.Playing)
		{
			BossMusic.Stop();
		}
		
		// ✅ Emitir señal de derrota
		EmitSignal(SignalName.BossDefeated);
		
		SetCollisionLayerValue(1, false);
		SetCollisionMaskValue(1, false);
		
		if (_collisionShape != null)
			_collisionShape.Disabled = true;
		
		HurtBox.Monitoring = false;
		HurtBox.Monitorable = false;
		
		anim.Play("BossDeath");
		Velocity = Vector2.Zero;
		GD.Print("💀 ¡BOSS DERROTADO!");
	}
	
	private void CancelAllAttacks()
	{
		MeleeHitbox.Monitoring = false;
		HeavyHitbox.Monitoring = false;
		ChargeHitbox.Monitoring = false;
		_chargeTimer = 0f;
		
		// 🔊 Detener sonido de charging si está activo
		if (SoundCharging != null && SoundCharging.Playing)
		{
			SoundCharging.Stop();
		}
	}
	
	private void OnMeleeHitboxBodyEntered(Node2D body)
	{
		if (body.IsInGroup("Player"))
		{
			Player player = body as Player;
			if (player != null)
			{
				GD.Print($"💥 Boss golpeó con Melee Attack ({MeleeDamage} de daño)");
				player.TakeDamage(MeleeDamage);
			}
		}
	}
	
	private void OnHeavyHitboxBodyEntered(Node2D body)
	{
		if (body.IsInGroup("Player"))
		{
			Player player = body as Player;
			if (player != null)
			{
				GD.Print($"💥 Boss golpeó con Heavy Attack ({HeavyDamage} de daño)");
				player.TakeDamage(HeavyDamage);
			}
		}
	}
	
	private void OnChargeHitboxBodyEntered(Node2D body)
	{
		if (body.IsInGroup("Player"))
		{
			Player player = body as Player;
			if (player != null)
			{
				GD.Print($"💥 Boss golpeó con Charge Attack ({ChargeDamage} de daño)");
				player.TakeDamage(ChargeDamage);
			}
		}
	}
	
	// ========== ANIMACIÓN CALLBACKS ==========
	private void OnAnimationFinished()
	{
		switch (anim.Animation)
		{
			case "BossMeleeAttack":
				MeleeHitbox.Monitoring = false;
				_attackCooldowns["melee"] = MeleeAttackCooldown;
				ChangeState(BossState.Idle);
				break;
				
			case "BossHeavyAttack":
				HeavyHitbox.Monitoring = false;
				_attackCooldowns["heavy"] = HeavyAttackCooldown;
				ChangeState(BossState.Idle);
				break;
				
			case "BossChargePrep":
				ChangeState(BossState.Charging);
				_chargeTimer = ChargeDuration;
				ChargeHitbox.Monitoring = true;
				anim.Play("BossCharging");
				
				// 🔊 Sonido de dash/carga
				PlaySound(SoundCharging);
				break;
				
			case "BossProjectileAttack":
				_attackCooldowns["projectile"] = ProjectileAttackCooldown;
				ChangeState(BossState.Idle);
				break;
				
			case "BossGroundSlam":
				_attackCooldowns["groundslam"] = GroundSlamCooldown;
				PerformGroundSlamDamage();
				ChangeState(BossState.Idle);
				break;
				
			case "BossHurt":
				_isHurt = false;
				_isKnockedBack = false;
				ChangeState(BossState.Idle);
				break;
				
			case "BossDeath":
				QueueFree();
				break;
		}
	}
	
	private void OnFrameChanged()
	{
		string currentAnim = anim.Animation;
		int frame = anim.Frame;
		
		// Activar hitboxes en frames específicos
		if (currentAnim == "BossMeleeAttack")
		{
			if (frame == MeleeAttackFrameStart)
				MeleeHitbox.Monitoring = true;
			else if (frame == MeleeAttackFrameEnd)
				MeleeHitbox.Monitoring = false;
		}
		else if (currentAnim == "BossHeavyAttack")
		{
			if (frame == HeavyAttackFrameStart)
				HeavyHitbox.Monitoring = true;
			else if (frame == HeavyAttackFrameEnd)
				HeavyHitbox.Monitoring = false;
		}
		else if (currentAnim == "BossProjectileAttack" && frame == 10)
		{
			// Lanzar proyectil en el frame 10
			SpawnProjectile();
		}
		// 🔊 Sonido de pasos al caminar (cada ciertos frames)
		else if (currentAnim == "BossWalk" && (frame == 0 || frame == 4))
		{
			PlaySound(SoundWalk);
		}
	}
	
	// ========== ATAQUES ESPECIALES ==========
	private void SpawnProjectile()
	{
		if (ProjectileScene == null || _player == null) return;
		
		// 🔊 Sonido de disparo
		PlaySound(SoundProjectile);
		
		var projectile = ProjectileScene.Instantiate<Node2D>();
		GetTree().Root.AddChild(projectile);
		projectile.GlobalPosition = ProjectileSpawnPoint.GlobalPosition;
		
		// Calcular dirección hacia el jugador
		Vector2 direction = (_player.GlobalPosition - projectile.GlobalPosition).Normalized();
		
		// Si el proyectil tiene un método SetDirection, llamarlo
		if (projectile.HasMethod("SetDirection"))
		{
			projectile.Call("SetDirection", direction, ProjectileSpeed);
		}
		
		// ✅ Configurar daño del proyectil
		if (projectile.HasMethod("SetDamage"))
		{
			projectile.Call("SetDamage", ProjectileDamage);
		}
	}
	
	private void PerformGroundSlamDamage()
	{
		if (_player == null) return;
		
		float distanceToPlayer = GlobalPosition.DistanceTo(_player.GlobalPosition);
		
		if (distanceToPlayer <= GroundSlamRadius)
		{
			Player player = _player as Player;
			if (player != null)
			{
				GD.Print($"💥 Ground Slam golpeó al jugador ({GroundSlamDamage} de daño)");
				player.TakeDamage(GroundSlamDamage);
			}
		}
	}
	
	// ========== ACTUALIZACIÓN DE DIRECCIÓN ==========
	private void UpdateFacingDirection(bool shouldFaceRight)
	{
		_facingRight = shouldFaceRight;
		anim.FlipH = _facingRight;
		
		float scaleX = _facingRight ? -1 : 1;
		MeleeHitbox.Scale = new Vector2(scaleX, 1);
		HeavyHitbox.Scale = new Vector2(scaleX, 1);
		ChargeHitbox.Scale = new Vector2(scaleX, 1);
		HurtBox.Scale = new Vector2(scaleX, 1);
		
		// Actualizar spawn point de proyectiles
		if (ProjectileSpawnPoint != null)
		{
			float offset = _facingRight ? 50 : -50;
			ProjectileSpawnPoint.Position = new Vector2(offset, -20);
		}
	}
	
	private void ChangeState(BossState newState)
	{
		if (_currentState == newState) return;
		_currentState = newState;
	}
	
	// ========== SISTEMA DE DECISIÓN DE ATAQUES ==========
	private void DecideAttack(float distanceToPlayer)
	{
		// Lista de ataques disponibles
		List<string> availableAttacks = new List<string>();
		
		// Verificar qué ataques están listos
		if (_attackCooldowns["melee"] <= 0 && distanceToPlayer <= MeleeAttackRange)
			availableAttacks.Add("melee");
			
		if (_attackCooldowns["heavy"] <= 0 && distanceToPlayer <= MeleeAttackRange)
			availableAttacks.Add("heavy");
			
		if (_attackCooldowns["charge"] <= 0 && distanceToPlayer > MeleeAttackRange)
			availableAttacks.Add("charge");
			
		if (_attackCooldowns["projectile"] <= 0 && distanceToPlayer > MeleeAttackRange && distanceToPlayer <= RangedAttackRange)
			availableAttacks.Add("projectile");
			
		if (_attackCooldowns["groundslam"] <= 0 && _currentPhase >= 2)
			availableAttacks.Add("groundslam");
		
		// Si no hay ataques disponibles, solo caminar
		if (availableAttacks.Count == 0)
		{
			ChangeState(BossState.Walking);
			return;
		}
		
		// Elegir ataque aleatorio de los disponibles
		string chosenAttack = availableAttacks[(int)(GD.Randi() % availableAttacks.Count)];
		
		// Ejecutar el ataque elegido
		switch (chosenAttack)
		{
			case "melee":
				ChangeState(BossState.MeleeAttack);
				anim.Play("BossMeleeAttack");
				// 🔊 Sonido de ataque melee
				PlaySound(SoundMeleeAttack);
				break;
			case "heavy":
				ChangeState(BossState.HeavyAttack);
				anim.Play("BossHeavyAttack");
				// 🔊 Sonido de ataque pesado
				PlaySound(SoundHeavyAttack);
				break;
			case "charge":
				ChangeState(BossState.ChargePrep);
				_chargeDirection = (_player.GlobalPosition - GlobalPosition).Normalized();
				anim.Play("BossChargePrep");
				// 🔊 Sonido de preparación de carga
				PlaySound(SoundChargePrep);
				break;
			case "projectile":
				ChangeState(BossState.ProjectileAttack);
				anim.Play("BossProjectileAttack");
				break;
			case "groundslam":
				ChangeState(BossState.GroundSlam);
				anim.Play("BossGroundSlam");
				// 🔊 Sonido de ground slam
				PlaySound(SoundGroundSlam);
				break;
		}
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
		
		// Gravedad
		if (!IsOnFloor())
		{
			Velocity = new Vector2(Velocity.X, Velocity.Y + Gravity * d);
		}
		
		// Estado de herido
		if (_isHurt)
		{
			_hurtTimer -= d;
			if (_hurtTimer <= 0)
			{
				_isHurt = false;
				_isKnockedBack = false;
				ChangeState(BossState.Idle);
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
			
			// 🎵 INICIAR MÚSICA CUANDO EL JUGADOR SE ACERCA (SOLO UNA VEZ)
			if (!_musicStarted && distanceToPlayer <= AggroRange && BossMusic != null && BossMusic.Stream != null)
			{
				BossMusic.Play();
				_musicStarted = true;
				GD.Print("🎵 ¡Música de boss iniciada!");
			}
			
			// Actualizar dirección
			bool shouldFaceRight = _player.GlobalPosition.X > GlobalPosition.X;
			UpdateFacingDirection(shouldFaceRight);
			
			// Máquina de estados
			switch (_currentState)
			{
				case BossState.Idle:
					Velocity = new Vector2(0, Velocity.Y);
					anim.Play("BossIdle");
					
					// Decidir próxima acción
					if (distanceToPlayer <= AggroRange)
					{
						DecideAttack(distanceToPlayer);
					}
					break;
					
				case BossState.Walking:
					// Caminar hacia el jugador
					int moveSpeed = WalkSpeed;
					if (shouldFaceRight)
						Velocity = new Vector2(moveSpeed, Velocity.Y);
					else
						Velocity = new Vector2(-moveSpeed, Velocity.Y);
					
					anim.Play("BossWalk");
					
					if (distanceToPlayer <= MeleeAttackRange - 20)
					{
						DecideAttack(distanceToPlayer);
					}
					break;
					
				case BossState.Charging:
					// Dash rápido en la dirección guardada
					_chargeTimer -= d;
					
					if (_chargeTimer > 0)
					{
						Velocity = new Vector2(_chargeDirection.X * ChargeSpeed, Velocity.Y);
					}
					else
					{
						ChargeHitbox.Monitoring = false;
						ChangeState(BossState.Idle);
						_attackCooldowns["charge"] = ChargeAttackCooldown;
					}
					break;
					
				case BossState.MeleeAttack:
				case BossState.HeavyAttack:
				case BossState.ChargePrep:
				case BossState.ProjectileAttack:
				case BossState.GroundSlam:
					// No moverse durante ataques
					Velocity = new Vector2(0, Velocity.Y);
					break;
			}
		}
		else
		{
			// Sin jugador, idle
			Velocity = new Vector2(0, Velocity.Y);
			anim.Play("BossIdle");
		}
		
		MoveAndSlide();
	}
}
