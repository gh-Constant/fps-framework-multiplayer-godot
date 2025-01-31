using Godot;
using System;

public partial class DedicatedServer : Node
{
	[Export]
	public PackedScene GameWorldScene { get; set; }
	private Node3D _gameWorld;
	private MultiplayerSpawner _spawner;

	public override void _Ready()
	{
		try
		{
			if (GameWorldScene == null)
			{
				GD.PrintErr("GameWorldScene is not set in the DedicatedServer node!");
				return;
			}

			// Instantiate the game world
			_gameWorld = GameWorldScene.Instantiate<Node3D>();
			AddChild(_gameWorld);

			// Get the MultiplayerSpawner from the game world
			_spawner = _gameWorld.GetNode<MultiplayerSpawner>("MultiplayerSpawner");
			if (_spawner == null)
			{
				GD.PrintErr("Could not find MultiplayerSpawner in the GameWorld scene!");
				return;
			}

			// Get required nodes for NetworkManager
			var spawnPoints = _gameWorld.GetNode<Node3D>("SpawnPoints");
			var players = _gameWorld.GetNode<Node3D>("Players");

			if (spawnPoints == null || players == null)
			{
				GD.PrintErr("Could not find required nodes (SpawnPoints or Players) in the GameWorld scene!");
				return;
			}

			// Configure NetworkManager for dedicated server mode
			var networkManager = GetNode<NetworkManager>("/root/NetworkManager");
			networkManager.SetDedicatedServer(true);
			networkManager.SetupGame(spawnPoints, players);

			GD.Print("DedicatedServer: Successfully initialized game world and MultiplayerSpawner");
		}
		catch (Exception e)
		{
			GD.PrintErr($"Error in DedicatedServer._Ready: {e.Message}");
		}
	}
} 
