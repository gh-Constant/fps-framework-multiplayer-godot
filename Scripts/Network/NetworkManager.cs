using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class NetworkManager : Node
{
	[Signal]
	public delegate void ConnectionFailedEventHandler();

	[Signal]
	public delegate void ConnectedToServerEventHandler();

	private ENetMultiplayerPeer _peer;
	public const int DEFAULT_PORT = 7777;
	public const string DEFAULT_IP = "127.0.0.1";
	public const int MAX_PLAYERS = 32;
	private const float CONNECTION_TIMEOUT = 5.0f; // 5 seconds timeout
	
	private float _connectionTimer = 0;
	private bool _isConnecting = false;
	private string _pendingConnectionIp;
	private int _pendingConnectionPort;

	public static NetworkManager Instance { get; private set; }
	
	[Export]
	public PackedScene PlayerScene { get; set; }

	private Node3D _spawnPointsContainer;
	private Node _playersNode;
	private Random _random = new Random();
	private HashSet<int> _usedSpawnPoints = new HashSet<int>();

	public override void _Ready()
	{
		if (Instance == null)
		{
			Instance = this;
			GD.Print("NetworkManager initialized");
		}
		else
		{
			QueueFree();
			return;
		}

		Multiplayer.PeerConnected += OnPeerConnected;
		Multiplayer.PeerDisconnected += OnPeerDisconnected;
		Multiplayer.ConnectedToServer += OnConnectedToServer;
		Multiplayer.ConnectionFailed += OnConnectionFailed;
		
		if (PlayerScene == null)
		{
			GD.PrintErr("PlayerScene is not set in NetworkManager!");
		}
	}

	public override void _Process(double delta)
	{
		if (_isConnecting)
		{
			_connectionTimer += (float)delta;
			if (_connectionTimer >= CONNECTION_TIMEOUT)
			{
				GD.PrintErr($"Connection to {_pendingConnectionIp}:{_pendingConnectionPort} timed out");
				HandleConnectionTimeout();
			}
		}
	}

	private void HandleConnectionTimeout()
	{
		_isConnecting = false;
		_connectionTimer = 0;
		if (_peer != null)
		{
			_peer.Close();
			Multiplayer.MultiplayerPeer = null;
		}
		EmitSignal(SignalName.ConnectionFailed);
	}

	private void OnConnectedToServer()
	{
		_isConnecting = false;
		_connectionTimer = 0;
		GD.Print("Successfully connected to server");
		EmitSignal(SignalName.ConnectedToServer);
	}

	private void OnConnectionFailed()
	{
		_isConnecting = false;
		_connectionTimer = 0;
		GD.Print("Failed to connect to server");
		if (_peer != null)
		{
			_peer.Close();
			Multiplayer.MultiplayerPeer = null;
		}
		EmitSignal(SignalName.ConnectionFailed);
	}

	public override void _ExitTree()
	{
		if (Instance == this)
		{
			Multiplayer.PeerConnected -= OnPeerConnected;
			Multiplayer.PeerDisconnected -= OnPeerDisconnected;
			Multiplayer.ConnectedToServer -= OnConnectedToServer;
			Multiplayer.ConnectionFailed -= OnConnectionFailed;
			Instance = null;
		}
	}

	public void SetupGame(Node3D spawnPointsContainer, Node playersNode)
	{
		GD.Print("Setting up game with spawn points and players node");
		_spawnPointsContainer = spawnPointsContainer;
		_playersNode = playersNode;
		_usedSpawnPoints.Clear();
		
		// If we're the server, spawn our own player
		if (Multiplayer.IsServer())
		{
			GD.Print("We are the server, spawning host player");
			CallDeferred(nameof(SpawnPlayer), 1);
		}
	}

	public Error CreateHost(int port = DEFAULT_PORT)
	{
		GD.Print($"Creating host on port {port}");
		_peer = new ENetMultiplayerPeer();
		var error = _peer.CreateServer(port, MAX_PLAYERS);
		
		if (error != Error.Ok)
		{
			GD.PrintErr($"Failed to create server: {error}");
			return error;
		}

		Multiplayer.MultiplayerPeer = _peer;
		GD.Print($"Server started successfully on port {port}");
		return Error.Ok;
	}

	public Error JoinGame(string ip = DEFAULT_IP, int port = DEFAULT_PORT)
	{
		GD.Print($"Joining game at {ip}:{port}");
		_peer = new ENetMultiplayerPeer();
		var error = _peer.CreateClient(ip, port);
		
		if (error != Error.Ok)
		{
			GD.PrintErr($"Failed to create client: {error}");
			return error;
		}

		_isConnecting = true;
		_connectionTimer = 0;
		_pendingConnectionIp = ip;
		_pendingConnectionPort = port;
		Multiplayer.MultiplayerPeer = _peer;
		GD.Print($"Attempting to connect to {ip}:{port}");
		return Error.Ok;
	}

	public void DisconnectFromGame()
	{
		if (_peer != null)
		{
			_peer.Close();
			Multiplayer.MultiplayerPeer = null;
			GD.Print("Disconnected from game");
		}
	}

	private void OnPeerConnected(long id)
	{
		GD.Print($"Peer {id} connected");
		if (Multiplayer.IsServer())
		{
			GD.Print($"Spawning player for peer {id}");
			CallDeferred(nameof(SpawnPlayer), id);
		}
	}

	private void OnPeerDisconnected(long id)
	{
		GD.Print($"Peer {id} disconnected");
		var player = _playersNode?.GetNodeOrNull($"{id}");
		if (player != null)
		{
			player.QueueFree();
		}
	}

	private Vector3 GetRandomSpawnPoint()
	{
		if (_spawnPointsContainer == null || _spawnPointsContainer.GetChildCount() == 0)
		{
			GD.PrintErr("No spawn points available!");
			return Vector3.Zero;
		}

		var availablePoints = new List<int>();
		for (int i = 0; i < _spawnPointsContainer.GetChildCount(); i++)
		{
			if (!_usedSpawnPoints.Contains(i))
			{
				availablePoints.Add(i);
			}
		}

		// If all spawn points are used, clear the used list and start over
		if (availablePoints.Count == 0)
		{
			_usedSpawnPoints.Clear();
			availablePoints = Enumerable.Range(0, _spawnPointsContainer.GetChildCount()).ToList();
		}

		int spawnIndex = availablePoints[_random.Next(availablePoints.Count)];
		_usedSpawnPoints.Add(spawnIndex);

		var spawnPoint = _spawnPointsContainer.GetChild<Node3D>(spawnIndex);
		GD.Print($"Selected spawn point {spawnIndex} at position {spawnPoint.Position}");
		return spawnPoint.Position;
	}

	private void SpawnPlayer(long id)
	{
		if (PlayerScene == null)
		{
			GD.PrintErr("Cannot spawn player: PlayerScene is null");
			return;
		}
		
		if (_playersNode == null)
		{
			GD.PrintErr("Cannot spawn player: PlayersNode is null");
			return;
		}
		
		if (_spawnPointsContainer == null)
		{
			GD.PrintErr("Cannot spawn player: SpawnPoints container is null");
			return;
		}

		try
		{
			var spawnPosition = GetRandomSpawnPoint();
			var player = PlayerScene.Instantiate<Node3D>();
			player.Name = id.ToString();
			player.Position = spawnPosition;
			_playersNode.AddChild(player, true);
			player.SetMultiplayerAuthority((int)id);
			GD.Print($"Player {id} spawned successfully at {spawnPosition}");
		}
		catch (Exception e)
		{
			GD.PrintErr($"Error spawning player: {e.Message}");
		}
	}
} 
