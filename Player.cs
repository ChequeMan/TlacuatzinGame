using Godot;
using System;

public partial class Player : CharacterBody2D
{
	[Export] public int Speed { get; set; } = 300;
	[Export] public int JumpForce { get; set; } = -1600;
	[Export] public int Gravity { get; set; } = 4000;
	
	[Export] public int PlayerAttackFrameStart = 2;
	[Export] public int PlayerAttackFrameEnd = 5;
	
	// ✅ NUEVA: Configuración de desaceleración
	[Export] public float GroundFriction = 20f; // Fricción en el suelo (más alto = más rápido para)
	[Export] public float AirFriction = 8f; // Fricción en el aire
	
	// ✅ SISTEMA DE VIDA
	[Export] public int MaxHealth { get; set; } = 3;
	private int currentHealth;
	private bool isInvulnerable = false;
	private float invulnerabilityTime = 1.5f;
	private float invulnerabilityTimer = 0f;
	
	// 🎮 CONFIGURACIÓN DE VIBRACIONES
	[ExportGroup("Vibration Settings")]
	[Export] public float HitVibrationWeak = 0.3f;
	[Export] public float HitVibrationStrong = 0.5f;
	[Export] public float HitVibrationDuration = 0.15f;
	
	[Signal]
	public delegate void HealthChangedEventHandler(int currentHealth, int maxHealth);

	private AnimatedSprite2D anim;
	private AudioStreamPlayer2D stepSound;
	private AudioStreamPlayer2D Garras;
	private AudioStreamPlayer2D DeadFall;
	private Area2D PlayerAttackBox;
	private CollisionShape2D FistCollider;

	// Sistema de Dash
	private float dashTime = 0.2f;
	private bool isDashing = false;
	private float dashTimer = 0f;
	private int dashSpeed = 900;
	private bool dashAvailable = true;
	private int dashDirection = 0;
	
	// ✅ Sistema de Doble Salto
	private bool hasDoubleJump = true;
	private bool usedDoubleJump = false;

	private SnowArea SnowArea;
	private DangerousCave DangerousCave;
	private Camera2d Camara;

	private enum PlayerState { Idle, Run, PreJump, Jump, Fall, Land, Dash, AttackAir, AttackGround, Dead }
	private PlayerState currentState = PlayerState.Idle;

	private int framesSinceGrounded = 0;
	private int framesGroundedThreshold = 3;
	private float coyoteTime = 0.12f;
	private float coyoteTimer = 0f;

	private float jumpBufferTime = 0.12f;
	private float jumpBufferTimer = 0f;

	private float attackBufferTime = 0.12f;
	private float attackBufferTimer = 0f;

	private bool walkFastLeft = false;
	private bool walkFastRight = false;
	private float doubleTapWindow = 0.25f;
	private float lastTapTimeLeft = -1f;
	private float lastTapTimeRight = -1f;

	string walkTlacuachil;
	string idleTlacuachil;

	private bool isAttacking = false;
	private bool wasAirAttacking = false;
	private bool _inputEnabled = true;

	public override void _Ready()
	{
		currentHealth = MaxHealth;
		EmitSignal(SignalName.HealthChanged, currentHealth, MaxHealth);
		
		anim = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		anim.AnimationFinished += OnAnimationFinished;
		
		stepSound = GetNode<AudioStreamPlayer2D>("StepSound");
		Garras = GetNode<AudioStreamPlayer2D>("Garras");
		DeadFall = GetNode<AudioStreamPlayer2D>("FallDead");
		
		PlayerAttackBox = GetNode<Area2D>("PlayerAttackBox");
		PlayerAttackBox.Monitoring = false;
		PlayerAttackBox.BodyEntered += OnPlayerAttackBoxBodyEntered;
		
		FistCollider = GetNode<CollisionShape2D>("FistCollider");
		FistCollider.Disabled = true;
		
		anim.FrameChanged += OnPlayerFrameChanged;

		AddToGroup("Player");
		
		walkTlacuachil = "walkTlacuachil";
		idleTlacuachil = "idleTlacuachil";
		
		if (GetTree().Root.HasNode("World/Snow/SnowArea"))
		{
			SnowArea = GetTree().Root.GetNode<SnowArea>("World/Snow/SnowArea");
			SnowArea.PlayerEntered += OnSnowAreaEntered;
			SnowArea.PlayerExited += OnSnowAreaExited;
		}
		
		if (GetTree().Root.HasNode("World/DangerousCave"))
		{
			DangerousCave = GetTree().Root.GetNode<DangerousCave>("World/DangerousCave");
			DangerousCave.PlayerisDead += OnPlayerIsDead;
		}
		
		if (GetTree().Root.HasNode("World/Camera2D"))
		{
			Camara = GetTree().Root.GetNode<Camera2d>("World/Camera2D");
		}
		
		FloorStopOnSlope = true;
		FloorMaxAngle = Mathf.DegToRad(46);
		FloorSnapLength = 8.0f;
	}

	private void TriggerVibration(float weakMotor, float strongMotor, float duration)
	{
		if (Input.GetConnectedJoypads().Count > 0)
		{
			Input.StartJoyVibration(0, weakMotor, strongMotor, duration);
		}
	}

	public void TakeDamage(int damage)
	{
		if (currentState == PlayerState.Dead || isInvulnerable)
			return;
		
		currentHealth -= damage;
		GD.Print($"Player recibió {damage} de daño. Vida restante: {currentHealth}/{MaxHealth}");
		
		TriggerVibration(0.7f, 0.8f, 0.3f);
		
		isInvulnerable = true;
		invulnerabilityTimer = invulnerabilityTime;
		
		StartInvulnerabilityFlash();
		EmitSignal(SignalName.HealthChanged, currentHealth, MaxHealth);
		if (currentHealth <= 0)
		{
			Die();
		}
	}
	
	private async void StartInvulnerabilityFlash()
	{
		float flashDuration = invulnerabilityTime;
		float elapsed = 0f;
		
		while (elapsed < flashDuration && isInvulnerable)
		{
			anim.Modulate = new Color(1, 1, 1, 0.5f);
			await ToSignal(GetTree().CreateTimer(0.1f), "timeout");
			
			anim.Modulate = new Color(1, 1, 1, 1.0f);
			await ToSignal(GetTree().CreateTimer(0.1f), "timeout");
			
			elapsed += 0.2f;
		}
		
		anim.Modulate = new Color(1, 1, 1, 1.0f);
	}

	private void OnPlayerIsDead()
	{
		currentHealth = 0;
		_inputEnabled = false;
		if (Camara != null)
			Camara.StartZoom();
		Garras.Play();
		anim.Play("CanonDeadCave");
		if (!Garras.Playing)
			DeadFall.Play();
		ChangeState(PlayerState.Dead);
	}
	
	public void Die()
	{
		if (currentState == PlayerState.Dead) return;
		
		currentHealth = 0;
		_inputEnabled = false;
		ChangeState(PlayerState.Dead);
		
		if (Garras != null)
			Garras.Play();
		
		if (anim != null)
			anim.Play("CanonDeadCave");
		
		Velocity = Vector2.Zero;
		
		if (Camara != null)
			Camara.StartZoom();
		
		//GD.Print("Player ha muerto");
		GD.Print($"💀 Player ha muerto en posición: {GlobalPosition}");
		
		//////////novedad checkpoint
		if(CheckpointManager.Instance != null)
		{
			CheckpointManager.Instance.RespawnPlayer(this);
		}
		else
		{
			GD.PrintErr("CheckpointManager no encontrado! Agrega CheckpointManger como Autoload. ");
		}
	}
	
	public bool IsAttacking()
	{
		return isAttacking && PlayerAttackBox.Monitoring;
	}
	
	public int GetCurrentHealth()
	{
		return currentHealth;
	}
	
	public void Heal(int amount)
	{
		currentHealth = Mathf.Min(currentHealth + amount, MaxHealth);
		GD.Print($"Player curado. Vida actual: {currentHealth}/{MaxHealth}");
		EmitSignal(SignalName.HealthChanged, currentHealth, MaxHealth);
	}

	private void OnSnowAreaEntered()
	{
		walkTlacuachil = "snowWalk";
		idleTlacuachil = "IdleSnow";
		stepSound = GetNode<AudioStreamPlayer2D>("StepSound");
		GD.Print("✅ Jugador entró al área de nieve - Cambiando a animaciones de nieve");
	}

	private void OnSnowAreaExited()
	{
		walkTlacuachil = "walkTlacuachil";
		idleTlacuachil = "idleTlacuachil";
		stepSound = GetNode<AudioStreamPlayer2D>("PasoSeco");
		GD.Print("✅ Jugador salió del área de nieve - Cambiando a animaciones normales");
	}
	
	private void OnPlayerAttackBoxBodyEntered(Node2D body)
	{
		if (body is Enemy enemy)
		{
			GD.Print("¡Es un enemigo! Aplicando daño...");
			Vector2 knockbackDirection = (enemy.GlobalPosition - GlobalPosition).Normalized();
			enemy.TakeDamage(1, knockbackDirection);
			TriggerVibration(HitVibrationWeak, HitVibrationStrong, HitVibrationDuration);
		}
		
		if (body.IsInGroup("Boss"))
		{
			GD.Print("¡Es el BOSS! Aplicando daño...");
			if (body.HasMethod("TakeDamage"))
			{
				Vector2 knockbackDirection = (body.GlobalPosition - GlobalPosition).Normalized();
				body.Call("TakeDamage", 1, knockbackDirection);
				TriggerVibration(HitVibrationStrong, HitVibrationStrong + 0.2f, HitVibrationDuration + 0.05f);
			}
		}
	}
	
	private void OnPlayerFrameChanged()
	{
		if (anim.Animation == "Boxing")
		{
			int currentFrame = anim.Frame;
			
			if (currentFrame == PlayerAttackFrameStart)
			{
				PlayerAttackBox.Monitoring = true;
				FistCollider.Disabled = false;
			}
			else if (currentFrame == PlayerAttackFrameEnd)
			{
				PlayerAttackBox.Monitoring = false;
				FistCollider.Disabled = true;
			}
		}
	}
	
	private void OnAnimationFinished()
	{
		if (anim.Animation == "Boxing")
		{
			isAttacking = false;
			PlayerAttackBox.Monitoring = false;
			
			if (IsGrounded()) 
			{
				wasAirAttacking = false;
				if (currentState == PlayerState.AttackAir || currentState == PlayerState.AttackGround)
					ChangeState(PlayerState.Idle);
			}
			UpdateAnimationFromState();
		}

		if (currentState == PlayerState.PreJump && anim.Animation == "JumpPre")
		{
			var vel = Velocity;
			vel.Y = JumpForce;
			Velocity = vel;
			ChangeState(PlayerState.Jump);
			UpdateAnimationFromState();
		}

		if (currentState == PlayerState.Land && anim.Animation == "Landing")
		{
			ChangeState(PlayerState.Idle);
			UpdateAnimationFromState();
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!_inputEnabled) return;
		float d = (float)delta;

		if (isInvulnerable)
		{
			invulnerabilityTimer -= d;
			if (invulnerabilityTimer <= 0)
			{
				isInvulnerable = false;
				anim.Modulate = new Color(1, 1, 1, 1.0f);
			}
		}

		if (jumpBufferTimer > 0) jumpBufferTimer -= d;
		if (attackBufferTimer > 0) attackBufferTimer -= d;
		if (coyoteTimer > 0) coyoteTimer -= d;

		if (isDashing)
		{
			dashTimer -= d;
			if (dashTimer <= 0)
			{
				isDashing = false;
				dashDirection = 0;
			}
		}

		HandleInputBuffers();

		var velocity = Velocity;
		
		HandleDashInput();
		
		float targetVelX = CalculateHorizontalVelocity();
		velocity = ApplyGravity(d, velocity);

		if (isDashing)
		{
			velocity.X = dashDirection * dashSpeed;
		}
		else if (targetVelX != 0)
		{
			velocity.X = targetVelX;
		}
		else
		{
			// ✅ MEJOR DESACELERACIÓN
			float friction = IsGrounded() ? Speed * GroundFriction : Speed * AirFriction;
			velocity.X = Mathf.MoveToward(velocity.X, 0, friction * d);
		}

		Velocity = velocity;
		MoveAndSlide();
		velocity = Velocity;

		if (IsOnCeiling())
		{
			velocity.Y = 0;
			Velocity = velocity;
		}

		UpdateGroundedState(d);
		ResolveStateTransitions();
		UpdateAnimationFromState();
		HandleStepSound();
	}

	private void HandleInputBuffers()
	{
		if (Input.IsActionJustPressed("Ataque"))
		{
			attackBufferTimer = attackBufferTime;
		}

		if (Input.IsActionJustPressed("jump"))
		{
			jumpBufferTimer = jumpBufferTime;
		}

		if (attackBufferTimer > 0 && !isAttacking && currentState != PlayerState.Dead)
		{
			bool air = !IsGrounded();
			StartAttack(air);
			attackBufferTimer = 0;
		}

		// ✅ SALTO CORREGIDO - Ahora salta sin necesidad de presionar direcciones
		if (jumpBufferTimer > 0 && (IsGrounded() || coyoteTimer > 0) && currentState != PlayerState.Dead)
		{
			bool isMoving = Input.IsActionPressed("move_right") || Input.IsActionPressed("move_left");
			
			// ✅ Si está en movimiento O si tiene velocidad horizontal, salta inmediatamente
			if (isMoving || Mathf.Abs(Velocity.X) > 10f)
			{
				ChangeState(PlayerState.Jump);
				var vel = Velocity;
				vel.Y = JumpForce;
				Velocity = vel;
				UpdateAnimationFromState();
			}
			else
			{
				// ✅ Si está completamente quieto, usa PreJump pero TAMBIÉN aplica la fuerza
				ChangeState(PlayerState.PreJump);
				var vel = Velocity;
				vel.Y = JumpForce;
				Velocity = vel;
				anim.Play("JumpPre");
			}
			
			jumpBufferTimer = 0;
			coyoteTimer = 0;
			usedDoubleJump = false;
		}
		// ✅ DOBLE SALTO (en el aire)
		else if (jumpBufferTimer > 0 && !IsGrounded() && hasDoubleJump && !usedDoubleJump && currentState != PlayerState.Dead)
		{
			ChangeState(PlayerState.Jump);
			var vel = Velocity;
			vel.Y = JumpForce;
			Velocity = vel;
			UpdateAnimationFromState();
			
			jumpBufferTimer = 0;
			usedDoubleJump = true;
			GD.Print("¡DOBLE SALTO!");
		}
	}

	private void HandleDashInput()
	{
		if (!Input.IsActionJustPressed("Dash") || !dashAvailable || currentState == PlayerState.Dead)
			return;

		int direction = 0;
		if (Input.IsActionPressed("move_left"))
			direction = -1;
		else if (Input.IsActionPressed("move_right"))
			direction = 1;
		else
			direction = anim.FlipH ? -1 : 1;

		isDashing = true;
		dashTimer = dashTime;
		dashDirection = direction;
		dashAvailable = false;
		
		if (!IsGrounded())
		{
			var vel = Velocity;
			vel.Y = 0;
			Velocity = vel;
		}
		
		ChangeState(PlayerState.Dash);
	}

	private void StartAttack(bool air)
	{
		isAttacking = true;
		wasAirAttacking = air;
		ChangeState(air ? PlayerState.AttackAir : PlayerState.AttackGround);
		anim.Play("Boxing");
	}

	private float CalculateHorizontalVelocity()
	{
		float now = Time.GetTicksMsec() / 1000f;

		if (Input.IsActionJustPressed("move_left"))
		{
			if (now - lastTapTimeLeft <= doubleTapWindow) walkFastLeft = true;
			lastTapTimeLeft = now;
		}
		if (Input.IsActionJustPressed("move_right"))
		{
			if (now - lastTapTimeRight <= doubleTapWindow) walkFastRight = true;
			lastTapTimeRight = now;
		}

		if (Input.IsActionJustReleased("move_left")) walkFastLeft = false;
		if (Input.IsActionJustReleased("move_right")) walkFastRight = false;

		float target = 0;
		if (Input.IsActionPressed("move_left")) target = walkFastLeft ? -Speed * 3 : -Speed;
		else if (Input.IsActionPressed("move_right")) target = walkFastRight ? Speed * 3 : Speed;

		if (currentState == PlayerState.Jump || currentState == PlayerState.Fall || currentState == PlayerState.AttackAir)
		{
			if (Input.IsActionPressed("move_right")) target = Mathf.Max(target, Speed);
			else if (Input.IsActionPressed("move_left")) target = Mathf.Min(target, -Speed);
		}

		return target;
	}

	private Vector2 ApplyGravity(float delta, Vector2 velocity)
	{
		if (isDashing)
		{
			velocity.Y = 0;
			return velocity;
		}
		
		float multiplier = 1.0f;
		
		if (velocity.Y < 0)
		{
			if (!Input.IsActionPressed("jump")) multiplier = 1.2f;
			else multiplier = 0.9f;
		}
		else
		{
			multiplier = 1.6f;
		}

		velocity.Y += Gravity * delta * multiplier;
		return velocity;
	}

	private void UpdateGroundedState(float delta)
	{
		if (IsOnFloor())
		{
			framesSinceGrounded = 0;
			coyoteTimer = coyoteTime;
			if (!dashAvailable)
				dashAvailable = true;
			if (usedDoubleJump)
				usedDoubleJump = false;
		}
		else
		{
			framesSinceGrounded++;
		}
	}

	private bool IsGrounded()
	{
		return framesSinceGrounded < framesGroundedThreshold || coyoteTimer > 0;
	}

	private void ResolveStateTransitions()
	{
		if (currentState == PlayerState.Dead) return;

		if (isAttacking)
		{
			if (IsGrounded() && wasAirAttacking)
			{
				wasAirAttacking = false;
			}
			return;
		}

		if (isDashing) 
		{
			if (currentState != PlayerState.Dash)
				ChangeState(PlayerState.Dash);
			return;
		}

		if (!IsGrounded())
		{
			if (Velocity.Y < 0) ChangeState(PlayerState.Jump);
			else ChangeState(PlayerState.Fall);
			return;
		}
		else
		{
			if (Mathf.Abs(Velocity.X) > 1f) ChangeState(PlayerState.Run);
			else ChangeState(PlayerState.Idle);
		}
	}

	private void ChangeState(PlayerState newState)
	{
		if (currentState == newState) return;
		currentState = newState;
	}
	
	private void UpdatePlayerAttackBoxDirection()
	{
		float scaleX = anim.FlipH ? -1 : 1;
		PlayerAttackBox.Scale = new Vector2(scaleX, 1);
	}

	private void UpdateAnimationFromState()
	{
		if (Velocity.X != 0)
		{
			anim.FlipH = Velocity.X < 0;
			UpdatePlayerAttackBoxDirection();
		}

		if (isAttacking)
		{
			if (anim.Animation != "Boxing")
				anim.Play("Boxing");
			return;
		}

		if (currentState == PlayerState.PreJump) return;
		
		if (currentState == PlayerState.Land)
		{
			if (anim.Animation != "Landing") anim.Play("Landing");
			return;
		}

		switch (currentState)
		{
			case PlayerState.Jump:
				if (anim.Animation != "JumpFly") anim.Play("JumpFly");
				break;
			case PlayerState.Fall:
				if (anim.Animation != "Fall") anim.Play("Fall");
				break;
			case PlayerState.Run:
				if (anim.Animation != walkTlacuachil) anim.Play(walkTlacuachil);
				break;
			case PlayerState.Idle:
				if (anim.Animation != idleTlacuachil) anim.Play(idleTlacuachil);
				break;
		}
	}

	private void Land()
	{
		if (wasAirAttacking && isAttacking)
		{
			wasAirAttacking = false;
			return;
		}

		ChangeState(PlayerState.Land);
		isDashing = false;
		dashTimer = 0f;
		dashDirection = 0;

		bool isMoving = Input.IsActionPressed("move_right") || Input.IsActionPressed("move_left");

		TriggerVibration(0.5f, 1.0f, 0.1f);

		if (isMoving)
		{
			ChangeState(PlayerState.Run);
			anim.Play(walkTlacuachil);
		}
		else
		{
			anim.Play("Landing");
		}
	}

	private void HandleStepSound()
	{
		if (Mathf.Abs(Velocity.X) > 0.1f && IsGrounded() && currentState != PlayerState.Idle)
		{
			if (!stepSound.Playing) stepSound.Play();
		}
		else
		{
			if (stepSound.Playing) stepSound.Stop();
		}
	}
	public void ResetPlayerState()
	{
		// Resetear estado de muerte
		currentState = PlayerState.Idle;
		_inputEnabled = true;
		isInvulnerable = false;
		invulnerabilityTimer = 0f;
		
		// Resetear animación
		if(anim != null)
		{
			anim.Modulate = new Color(1, 1, 1, 1.0f);
			anim.Play(idleTlacuachil);
		}
		// Resetear cámara si tienes el método
		//if (Camara != null && Camara.HasMethod("ResetZoom"))
		if(Camara != null)
		Camara.ResetZoom();//comentar de nuevo si crashea
	
	// Resetear mecánicas
	isDashing = false;
	dashTimer = 0f;
	dashAvailable = true;
	isAttacking = false;
	wasAirAttacking = false;
	usedDoubleJump = false;
	framesSinceGrounded = 0;
	
	GD.Print("Estado del jugador completamente reseteado");
	}
}
