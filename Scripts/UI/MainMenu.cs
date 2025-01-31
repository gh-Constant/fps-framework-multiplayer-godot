using Godot;
using System;

public partial class MainMenu : Control
{
    private LineEdit _ipAddress;
    private LineEdit _port;
    private Button _hostButton;
    private Button _joinButton;
    private Label _statusLabel;
    private Label _ipLabel;

    [Export]
    public PackedScene GameWorld { get; set; }

    public override void _Ready()
    {
        _ipAddress = GetNode<LineEdit>("%IpAddress");
        _port = GetNode<LineEdit>("%Port");
        _hostButton = GetNode<Button>("%HostButton");
        _joinButton = GetNode<Button>("%JoinButton");
        _statusLabel = GetNode<Label>("%StatusLabel");
        _ipLabel = GetNode<Label>("%IpLabel");

        // Set default values
        _ipAddress.Text = NetworkManager.DEFAULT_IP;
        _port.Text = NetworkManager.DEFAULT_PORT.ToString();

        // Connect signals
        _hostButton.Pressed += OnHostPressed;
        _joinButton.Pressed += OnJoinPressed;
        
        // Connect to NetworkManager signals
        var networkManager = GetNode<NetworkManager>("/root/NetworkManager");
        networkManager.Connect("ConnectionFailed", new Callable(this, nameof(OnConnectionFailed)));
        networkManager.Connect("ConnectedToServer", new Callable(this, nameof(OnConnectedToServer)));
    }

    private void OnHostPressed()
    {
        if (int.TryParse(_port.Text, out int port))
        {
            SetStatus("Starting server...", Colors.Yellow);
            var error = NetworkManager.Instance.CreateHost(port);
            if (error == Error.Ok)
            {
                SetStatus("Server started! Loading world...", Colors.Green);
                CallDeferred(nameof(LoadGameWorld));
            }
            else
            {
                SetStatus($"Failed to start server: {error}", Colors.Red);
            }
        }
        else
        {
            SetStatus("Invalid port number!", Colors.Red);
        }
    }

    private void OnJoinPressed()
    {
        if (int.TryParse(_port.Text, out int port))
        {
            SetStatus("Connecting to server...", Colors.Yellow);
            DisableButtons();
            var error = NetworkManager.Instance.JoinGame(_ipAddress.Text, port);
            if (error != Error.Ok)
            {
                SetStatus($"Failed to connect: {error}", Colors.Red);
                EnableButtons();
            }
        }
        else
        {
            SetStatus("Invalid port number!", Colors.Red);
        }
    }

    private void OnConnectionFailed()
    {
        CallDeferred(nameof(HandleConnectionFailed));
    }

    private void HandleConnectionFailed()
    {
        SetStatus("Connection failed or timed out!", Colors.Red);
        EnableButtons();
    }

    private void OnConnectedToServer()
    {
        CallDeferred(nameof(LoadGameWorld));
    }

    private void DisableButtons()
    {
        _hostButton.Disabled = true;
        _joinButton.Disabled = true;
        _ipAddress.Editable = false;
        _port.Editable = false;
    }

    private void EnableButtons()
    {
        _hostButton.Disabled = false;
        _joinButton.Disabled = false;
        _ipAddress.Editable = true;
        _port.Editable = true;
    }

    private void LoadGameWorld()
    {
        if (GameWorld == null)
        {
            SetStatus("Game world scene not set!", Colors.Red);
            return;
        }

        try
        {
            // Instance the game world
            var world = GameWorld.Instantiate<Node3D>();
            
            // Setup network manager with spawn points
            var spawnPoints = world.GetNode<Node3D>("SpawnPoints");
            var playersNode = world.GetNode<Node3D>("Players");
            
            if (spawnPoints == null || playersNode == null)
            {
                SetStatus("Failed to find required nodes in game world!", Colors.Red);
                return;
            }

            NetworkManager.Instance.SetupGame(spawnPoints, playersNode);

            // Switch to the game world scene
            GetTree().Root.AddChild(world);
            QueueFree();
        }
        catch (Exception e)
        {
            GD.PrintErr($"Error loading game world: {e.Message}");
            SetStatus($"Error loading game world: {e.Message}", Colors.Red);
        }
    }

    private void SetStatus(string message, Color color)
    {
        GD.Print($"Status: {message}");
        _statusLabel.Text = message;
        _statusLabel.Modulate = color;
    }
} 