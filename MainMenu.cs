using Godot;
using System;

public partial class MainMenu : Node2D
{
	private TextureButton startButton;
	private TextureButton optionsButton;
	private TextureButton quitButton;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		startButton = GetNode<TextureButton>("ContButton/Start");
		optionsButton = GetNode<TextureButton>("ContButton/Options");
		quitButton = GetNode<TextureButton>("ContButton/Quit");
		
		startButton.Pressed += OnStartButton;
		optionsButton.Pressed += OnOptionsButton;
		quitButton.Pressed += OnQuitButton;
	}
	private void OnStartButton()
	{
		GetTree().ChangeSceneToFile("res://level_1.tscn");
	}
	
	private void OnOptionsButton()
	{
		GetTree().ChangeSceneToFile("");
	}
	private void OnQuitButton()
	{
		GetTree().Quit();
	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
