using Godot;
using System;
using System.Collections.Generic;

public partial class JaguarBoss : CharacterBody2D
{
	// ========== CONFIGURACIÓN DE NIVEL ==========
	[ExportGroup("Boss Level")]
	[Export] public int BossLevel = 1; // 1, 2, o 3
	
	// ========== ESTADÍSTICAS BASE ==========
	[ExportGroup("Base Stats")]
	[Export] public int BaseMaxHealth = 20;
	[Export] public float BaseGravity = 980f;
	[Export] public int BaseWalkSpeed = 120;
	[Export] public int BaseChargeSpeed = 450;
	[Export] public float BaseKnockbackForce = 180f;
	
	// ========== RANGOS Y SENSORES ==========
	[ExportGroup("Ranges")]
	// Aggro: Distancia a la que te ve y empieza a perseguir
	[Export] public float AggroRange = 1500f; 
	// Distancia mínima para considerar hacer un Pounce (salto)
	[Export] public float MinPounceDistance = 400f;

	// ========== DEBUG ==========
	[ExportGroup("Debug")]
	[Export] public bool ShowDebugGizmos = true;
	
	// ========== CONFIGURACIÓN DE SALTO (POUNCE) ==========
	[ExportGroup("Pounce Settings")]
	[Export] public float PounceHorizontalSpeed = 450f;
	[Export] public float PounceVerticalSpeed = -600f;
	[Export] public float PounceGravityMultiplier = 0.6f;
	
	// ========== CONFIGURACIÓN DE SHADOW DASH ==========
	[ExportGroup("Shadow Dash Settings")]
	[Export] public float ShadowDashSpeed = 700f;
	[Export] public bool ShadowDashIgnoresGravity = true;
	
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
	[Export] public float BaseFuryCooldown = 8.0f;
	[Export] public float BaseShadowDashCooldown = 6.0f;
	
	// ========== COMPONENTES ==========
	private AnimatedSprite2D anim;
	private CollisionShape2D _collisionShape;
	
	// Hitboxes (Las que hacen daño)
	private Area2D ClawHitbox;
	private Area2D BiteHitbox;
	private Area2D PounceHitbox;
	private Area2D HurtBox;

	// SENSOR NUEVO (El que detecta si debe atacar)
	private Area2D _attackDetectionZone;
	private bool _playerInAttackZone = false;
	
	// ========== VARIABLES INTERNAS ==========
	private int _maxHealth;
	private int _currentHealth;
	private float _gravity;
	private int _walkSpeed;
	private int _chargeSpeed;
	private float _knockbackForce;
	
	// Cooldowns
	private float _clawCooldown;
	private float _biteCooldown;
	private float _pounceCooldown;
	private float _furyCooldown;
	private float _shadowDashCooldown;
	
	// Eventos
	public event Action<int, int> HealthChanged;
	public event Action BossDefeated;

	// Estado
	private enum JaguarState { Idle, Stalking, Running, ClawAttack, BiteAttack, Pouncing, PounceAir, FuryMode, ShadowDash, Hurt, Dead }
	private JaguarState _currentState = JaguarState.Idle;
	
	private bool _isDead = false;
	private bool _isHurt = false;
	private float _hurtTimer = 0f;
	private Vector2 _knockbackVelocity = Vector2.Zero;
	private bool _isKnockedBack = false;
	
	private CharacterBody2D _player;
	private bool _facingRight = true;
	
	private Dictionary<string, float> _attackCooldowns = new Dictionary<string, float>();
	
	// Variables Pounce/Fury/Dash
	private Vector2 _pounceVelocity = Vector2.Zero;
	private bool _isPouncing = false;
	private bool _isFuryMode = false;
	private float _furyTimer = 0f;
	private bool _isShadowDashing = false;
	private float _shadowDashTimer = 0f;
	private Vector2 _dashDirection = Vector2.Zero;

	public override void _Ready()
	{
		// Inicialización de componentes
		anim = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_collisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
		
		ClawHitbox = GetNode<Area2D>("ClawHitbox");
		ClawHitbox.Monitoring = false;
		ClawHitbox.BodyEntered += OnAttackHitboxBodyEntered; // Usamos un método genérico
		
		BiteHitbox = GetNode<Area2D>("BiteHitbox");
		BiteHitbox.Monitoring = false;
		BiteHitbox.BodyEntered += OnAttackHitboxBodyEntered;
		
		PounceHitbox = GetNode<Area2D>("PounceHitbox");
		PounceHitbox.Monitoring = false;
		PounceHitbox.BodyEntered += OnAttackHitboxBodyEntered;
		
		HurtBox = GetNode<Area2D>("HurtBox");
		HurtBox.BodyEntered += OnHurtBoxBodyEntered;

		// --- NUEVO SENSOR ---
		// Asegúrate de haber creado el Area2D llamado "AttackDetectionZone" en el editor
		_attackDetectionZone = GetNode<Area2D>("AttackDetectionZone");
		_attackDetectionZone.BodyEntered += (body) => {
			if (body.IsInGroup("Player")) _playerInAttackZone = true;
		};
		_attackDetectionZone.BodyExited += (body) => {
			if (body.IsInGroup("Player")) _playerInAttackZone = false;
		};
		
		// Señales de animación
		anim.AnimationFinished += OnAnimationFinished;
		anim.FrameChanged += OnFrameChanged;
		
		AddToGroup("Boss");
		ScaleStatsForLevel();
		InitializeCooldowns();
	}

	// ========== LÓGICA PRINCIPAL (PHYSICS PROCESS) ==========
	public override void _PhysicsProcess(double delta)
	{
		if (_isDead) return;
		float d = (float)delta;
		
		UpdateCooldowns(d);
		UpdateFuryMode(d);
		
		if (ShowDebugGizmos) QueueRedraw(); // Debug visual

		// 1. Manejo de Estados Especiales (Dash, Salto, Herido)
		if (HandleSpecialMovementStates(d)) return;

		// 2. Gravedad Normal
		if (!IsOnFloor()) Velocity = new Vector2(Velocity.X, Velocity.Y + _gravity * d);

		// 3. Buscar Jugador
		if (_player == null) {
			_player = GetTree().GetFirstNodeInGroup("Player") as CharacterBody2D;
			MoveAndSlide();
			return;
		}

		float distanceToPlayer = GlobalPosition.DistanceTo(_player.GlobalPosition);
		bool shouldFaceRight = _player.GlobalPosition.X > GlobalPosition.X;
		
		// Solo girar si no estamos atacando
		if (!IsAttackingState()) UpdateFacingDirection(shouldFaceRight);

		// 4. MÁQUINA DE ESTADOS
		switch (_currentState)
		{
			case JaguarState.Idle:
				Velocity = new Vector2(0, Velocity.Y);
				anim.Play("JaguarIdle");
				
				// Si el jugador entra en rango de visión
				if (distanceToPlayer <= AggroRange)
				{
					// Si ya está en la zona de ataque, atacar inmediatamente
					if (_playerInAttackZone) DecideMeleeAttack();
					else ChangeState(JaguarState.Running);
				}
				break;
				
			case JaguarState.Running:
	// 1. PRIORIDAD MÁXIMA: ¿Ya chocó con la zona de ataque?
	if (_playerInAttackZone)
	{
		// ¡FRENADO INSTANTÁNEO!
		Velocity = new Vector2(0, Velocity.Y); 
		
		// Forzar la decisión de ataque inmediatamente
		DecideMeleeAttack();
		
		// Importante: Usamos 'continue' o salimos del switch para 
		// que NO se ejecute el código de movimiento de abajo.
		break; 
	}

	// 2. Lógica normal de correr (solo si NO está en zona de ataque)
	
	// Checar si debe saltar (Pounce)
	if (distanceToPlayer > MinPounceDistance && _attackCooldowns["pounce"] <= 0)
	{
		ChangeState(JaguarState.Pouncing);
		anim.Play("JaguarPouncePrep");
	}
	// Checar si debe hacer Shadow Dash (Nivel 3)
	else if (BossLevel >= 3 && distanceToPlayer > 300 && _attackCooldowns["shadowdash"] <= 0)
	{
		ExecuteShadowDash();
	}
	// Si nada de lo anterior, CORRER hacia el jugador
	else
	{
		int speed = _isFuryMode ? (int)(_chargeSpeed * 0.9f) : _walkSpeed;
		float dir = shouldFaceRight ? 1 : -1;
		Velocity = new Vector2(speed * dir, Velocity.Y);
		anim.Play("JaguarRun");
	}
	break;
				
			// Los estados de ataque solo esperan a que termine la animación
			case JaguarState.ClawAttack:
			case JaguarState.BiteAttack:
			case JaguarState.Pouncing:
				Velocity = new Vector2(0, Velocity.Y); // Quieto mientras prepara ataque
				break;
		}
		
		MoveAndSlide();
	}

	// ========== SISTEMA DE ATAQUE (SIMPLIFICADO) ==========
	private void DecideMeleeAttack()
	{
		List<string> availableAttacks = new List<string>();
		
		if (_attackCooldowns["claw"] <= 0) availableAttacks.Add("claw");
		if (_attackCooldowns["bite"] <= 0) availableAttacks.Add("bite");
		
		if (availableAttacks.Count == 0)
		{
			// Si no hay ataques listos, esperar un poco en Idle
			ChangeState(JaguarState.Idle);
			return;
		}
		
		string chosen = availableAttacks[(int)(GD.Randi() % availableAttacks.Count)];
		
		if (chosen == "claw") {
			ChangeState(JaguarState.ClawAttack);
			anim.Play("JaguarClawAttack");
		} else {
			ChangeState(JaguarState.BiteAttack);
			anim.Play("JaguarBiteAttack");
		}
	}

	private void OnAttackHitboxBodyEntered(Node2D body)
	{
		if (body.IsInGroup("Player"))
		{
			Player player = body as Player;
			
			// CONFIRMAMOS QUE EL JUGADOR EXISTA Y NO ESTÉ YA MUERTO
			if (player != null && player.GetCurrentHealth() > 0)
			{
				// --- CORRECCIÓN AQUÍ ---
				// Antes decía: player.Die(); 
				// Ahora decimos: "Quítale 1 de vida"
				player.TakeDamage(1);
				
				// Opcional: Empujar al jugador (Knockback simple)
				// Como tu script de Player no tiene una función explicita de "ApplyKnockback",
				// el daño solo le bajará vida y lo hará parpadear (invulnerable).
			}
		}
	}

	// ========== ANIMACIÓN Y HITBOXES ==========
	private void OnFrameChanged()
	{
		string animName = anim.Animation;
		int frame = anim.Frame;
		
		// Sincronización exacta de Hitboxes con Frames
		if (animName == "JaguarClawAttack")
			ClawHitbox.Monitoring = (frame >= ClawAttackFrameStart && frame <= ClawAttackFrameEnd);
			
		else if (animName == "JaguarBiteAttack")
			BiteHitbox.Monitoring = (frame >= BiteAttackFrameStart && frame <= BiteAttackFrameEnd);
			
		else if (animName == "JaguarPounceAir")
			PounceHitbox.Monitoring = (frame >= PounceAttackFrameStart && frame <= PounceAttackFrameEnd);
	}

	private void OnAnimationFinished()
	{
		string animName = anim.Animation;
		
		if (animName == "JaguarClawAttack") {
			ClawHitbox.Monitoring = false;
			_attackCooldowns["claw"] = _clawCooldown;
			ChangeState(JaguarState.Idle);
		}
		else if (animName == "JaguarBiteAttack") {
			BiteHitbox.Monitoring = false;
			_attackCooldowns["bite"] = _biteCooldown;
			ChangeState(JaguarState.Idle);
		}
		else if (animName == "JaguarPouncePrep") {
			ExecutePounce(); // Saltar al terminar la preparación
		}
		else if (animName == "JaguarPounceAir") {
			_isPouncing = false;
			PounceHitbox.Monitoring = false;
			_attackCooldowns["pounce"] = _pounceCooldown;
			ChangeState(JaguarState.Idle);
		}
		else if (animName == "JaguarHurt" || animName == "JaguarShadowDash") {
			_isHurt = false;
			_isKnockedBack = false;
			ChangeState(JaguarState.Idle);
		}
	}

	// ========== UTILIDADES Y ESTADO ==========
	private bool HandleSpecialMovementStates(float d)
	{
		// Manejo de Shadow Dash (Movimiento horizontal puro)
		if (_isShadowDashing)
		{
			_shadowDashTimer -= d;
			if (_shadowDashTimer <= 0) {
				_isShadowDashing = false;
				Modulate = Colors.White;
				ChangeState(JaguarState.Idle);
			} else {
				if (ShadowDashIgnoresGravity) Velocity = _dashDirection * ShadowDashSpeed;
				else Velocity = new Vector2(_dashDirection.X * ShadowDashSpeed, Velocity.Y + _gravity * d);
				MoveAndSlide();
			}
			return true;
		}

		// Manejo de Pounce (Salto en el aire)
		if (_currentState == JaguarState.PounceAir && _isPouncing)
		{
			Velocity = _pounceVelocity;
			_pounceVelocity.Y += _gravity * d * PounceGravityMultiplier;
			
			if (IsOnFloor() && _pounceVelocity.Y > 0) // Aterrizó
			{
				_isPouncing = false;
				PounceHitbox.Monitoring = false;
				anim.Play("JaguarIdle"); // O una animación de aterrizaje
				ChangeState(JaguarState.Idle);
			}
			MoveAndSlide();
			return true;
		}

		// Manejo de Hurt (Knockback)
		if (_isHurt)
		{
			_hurtTimer -= d;
			if (_hurtTimer <= 0) {
				_isHurt = false;
				ChangeState(JaguarState.Idle);
			}
			if (_isKnockedBack) {
				Velocity = _knockbackVelocity;
				_knockbackVelocity = _knockbackVelocity.Lerp(Vector2.Zero, 0.1f);
			}
			MoveAndSlide();
			return true;
		}
		
		return false;
	}
	
	private bool IsAttackingState()
	{
		return _currentState == JaguarState.ClawAttack || 
			   _currentState == JaguarState.BiteAttack || 
			   _currentState == JaguarState.Pouncing || 
			   _currentState == JaguarState.PounceAir;
	}

	private void UpdateFacingDirection(bool shouldFaceRight)
	{
		if (_facingRight != shouldFaceRight)
		{
			_facingRight = shouldFaceRight;
			anim.FlipH = !_facingRight;
			
			// Al invertir la escala en X, todos los hijos (hitboxes, sensores) se invierten automáticamente
			// siempre y cuando estén bien centrados en el nodo padre.
			// Si usas escalas negativas directas en Scale.X:
			Vector2 currentScale = Scale;
			currentScale.X = Mathf.Abs(currentScale.X) * (_facingRight ? -1 : 1); 
			// Nota: Ajusta esto según cómo esté tu sprite original (si mira a la izq o der por defecto)
			
			// Si prefieres mover los hitboxes manualmente (más seguro con escalas complejas):
			float direction = !_facingRight ? -1 : 1;
			ClawHitbox.Scale = new Vector2(direction, 1);
			BiteHitbox.Scale = new Vector2(direction, 1);
			PounceHitbox.Scale = new Vector2(direction, 1);
			_attackDetectionZone.Scale = new Vector2(direction, 1);
		}
	}
	
	// ========== POUNCE Y DASH ==========
	private void ExecutePounce()
	{
		if (_player == null) return;
		Vector2 dir = (_player.GlobalPosition - GlobalPosition).Normalized();
		_pounceVelocity = new Vector2(dir.X * PounceHorizontalSpeed, PounceVerticalSpeed);
		_isPouncing = true;
		PounceHitbox.Monitoring = true;
		ChangeState(JaguarState.PounceAir);
		anim.Play("JaguarPounceAir");
	}
	
	private void ExecuteShadowDash()
	{
		if (_player == null) return;
		_isShadowDashing = true;
		_shadowDashTimer = 0.8f;
		Vector2 dir = (_player.GlobalPosition - GlobalPosition).Normalized();
		_dashDirection = new Vector2(dir.X, 0).Normalized();
		_attackCooldowns["shadowdash"] = _shadowDashCooldown;
		ChangeState(JaguarState.ShadowDash);
		anim.Play("JaguarShadowDash");
		Modulate = new Color(1, 1, 1, 0.3f);
	}
	
	// ========== COOLDOWNS Y DAÑO ==========
	private void UpdateCooldowns(float d)
	{
		List<string> keys = new List<string>(_attackCooldowns.Keys);
		foreach (var key in keys) {
			if (_attackCooldowns[key] > 0) _attackCooldowns[key] -= d;
		}
	}
	
	private void UpdateFuryMode(float d) {
		if (_isFuryMode) {
			_furyTimer -= d;
			if (_furyTimer <= 0) { _isFuryMode = false; Modulate = Colors.White; }
		}
	}
	
	private void InitializeCooldowns() {
		_attackCooldowns["claw"] = 0; _attackCooldowns["bite"] = 0;
		_attackCooldowns["pounce"] = 0; _attackCooldowns["shadowdash"] = 0;
	}

	private void ScaleStatsForLevel() {
		_maxHealth = BaseMaxHealth * BossLevel; // Ejemplo simple
		_currentHealth = _maxHealth;
		_walkSpeed = BaseWalkSpeed;
		_chargeSpeed = BaseChargeSpeed;
		_gravity = BaseGravity;
	}

	public void TakeDamage(int damage, Vector2 dir) {
		// Tu lógica de daño aquí (copiar del script anterior si la necesitas exacta)
		if (_isDead) return;
		_currentHealth -= damage;
		HealthChanged?.Invoke(_currentHealth, _maxHealth);
		if (_currentHealth <= 0) {
			_isDead = true;
			anim.Play("JaguarDeath");
			BossDefeated?.Invoke();
		} else {
			_isHurt = true;
			_hurtTimer = 0.3f;
			anim.Play("JaguarHurt");
		}
	}
	
	private void OnHurtBoxBodyEntered(Node2D body) {
		// Lógica para recibir daño del jugador
		if (body.IsInGroup("PlayerHitbox")) { /* Recibir daño */ }
	}

	// ========== DEBUG DIBUJO ==========
	public override void _Draw()
	{
		if (!ShowDebugGizmos) return;
		// Rango Agro
		DrawArc(Vector2.Zero, AggroRange, 0, Mathf.Tau, 32, Colors.Green, 2.0f);
		
		// Visualizar HITBOXES ACTIVAS
		if (ClawHitbox.Monitoring) DrawRectWithTransform(ClawHitbox, new Color(1, 0, 0, 0.6f));
		if (BiteHitbox.Monitoring) DrawRectWithTransform(BiteHitbox, new Color(1, 0.5f, 0, 0.6f));
		
		// Visualizar SENSOR DE ATAQUE (Siempre visible en debug)
		DrawRectWithTransform(_attackDetectionZone, new Color(0, 1, 1, 0.3f));
	}
	
	private void DrawRectWithTransform(Area2D area, Color color)
	{
		CollisionShape2D shape = area.GetNode<CollisionShape2D>("CollisionShape2D");
		if (shape != null && shape.Shape is RectangleShape2D rect) {
			Vector2 size = rect.Size;
			Vector2 offset = shape.Position - (size / 2);
			Vector2 finalPos = new Vector2(area.Position.X * area.Scale.X, area.Position.Y);
			DrawRect(new Rect2(finalPos + offset, size), color);
		}
	}
	// ========== MÉTODO QUE FALTABA ==========
	private void ChangeState(JaguarState newState)
	{
		// Si ya estamos en ese estado, no hacer nada
		if (_currentState == newState) return;
		
		_currentState = newState;
		
		// Opcional: Imprimir en consola para ver qué hace el boss
		// GD.Print($"🐆 Estado cambiado a: {newState}");
	}
}
