using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class CheckpointManager : Node
{
	private static CheckpointManager instance;
	public static CheckpointManager Instance => instance;
	
	[Export] public float RespawnDelay = 2.0f;
	
	// Lista de TODOS los checkpoints activados con sus posiciones
	private List<Vector2> activatedCheckpoints = new List<Vector2>();
	
	public override void _Ready()
	{
		if (instance != null && instance != this)
		{
			QueueFree();
			return;
		}
		
		instance = this;
		ProcessMode = ProcessModeEnum.Always; // Funciona incluso si pausas el juego
		GD.Print("✅ CheckpointManager (Autoload) inicializado");
	}
	
	// Registra un checkpoint cuando el jugador lo activa
	public void RegisterCheckpoint(Vector2 position)
	{
		if (!activatedCheckpoints.Contains(position))
		{
			activatedCheckpoints.Add(position);
			GD.Print($"📍 Checkpoint registrado en: {position} (Total: {activatedCheckpoints.Count})");
		}
	}
	
	// Encuentra el checkpoint MÁS CERCANO a la posición de muerte
	public Vector2 GetClosestCheckpoint(Vector2 deathPosition)
	{
		if (activatedCheckpoints.Count == 0)
		{
			GD.PrintErr("⚠️ No hay checkpoints activados! El jugador debe pasar por al menos uno.");
			return deathPosition; // Respawn en el mismo lugar si no hay checkpoints
		}
		
		// Buscar el checkpoint más cercano
		Vector2 closest = activatedCheckpoints[0];
		float minDistance = deathPosition.DistanceTo(closest);
		
		foreach (var checkpoint in activatedCheckpoints)
		{
			float distance = deathPosition.DistanceTo(checkpoint);
			if (distance < minDistance)
			{
				minDistance = distance;
				closest = checkpoint;
			}
		}
		
		GD.Print($"🎯 Checkpoint más cercano encontrado a {minDistance} unidades de distancia");
		return closest;
	}
	
	// Maneja el respawn del jugador
	public async void RespawnPlayer(Player player)
	{
		if (player == null) return;
		
		// Guardar posición de muerte
		Vector2 deathPosition = player.GlobalPosition;
		
		GD.Print($"💀 Jugador murió en: {deathPosition}");
		GD.Print($"⏳ Esperando {RespawnDelay} segundos para respawn...");
		
		await ToSignal(GetTree().CreateTimer(RespawnDelay), "timeout");
		
		// Encontrar checkpoint más cercano
		Vector2 respawnPosition = GetClosestCheckpoint(deathPosition);
		
		// Mover jugador
		player.GlobalPosition = respawnPosition;
		player.Velocity = Vector2.Zero;
		
		// Restaurar vida completa
		player.Heal(player.MaxHealth);
		
		// Resetear estado del jugador
		player.Call("ResetPlayerState");
		
		GD.Print($"✨ Jugador respawneado en: {respawnPosition}");
	}
	
	// Reinicia todos los checkpoints (útil al cambiar de nivel)
	public void ResetAllCheckpoints()
	{
		activatedCheckpoints.Clear();
		GD.Print("🔄 Todos los checkpoints han sido reseteados");
	}
	
	// Información de debug
	public int GetActiveCheckpointCount()
	{
		return activatedCheckpoints.Count;
	}
}
