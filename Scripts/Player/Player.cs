using Godot;
using System;

public partial class Player : CharacterBody3D
{
    [Export]
    public float Speed = 5.0f;
    
    [Export]
    public float JumpVelocity = 4.5f;

    private float _gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();
    private Node3D _head;
    private Camera3D _camera;
    private MultiplayerSynchronizer _sync;

    public override void _Ready()
    {
        _head = GetNode<Node3D>("%Head");
        _camera = GetNode<Camera3D>("%Camera");
        _sync = GetNode<MultiplayerSynchronizer>("%NetworkSync");

        // Only enable the camera for the local player
        if (!IsMultiplayerAuthority())
        {
            _camera.ClearCurrent();
            return;
        }

        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsMultiplayerAuthority()) return;

        if (@event is InputEventMouseMotion mouseMotion)
        {
            _head.RotateY(-mouseMotion.Relative.X * 0.005f);
            _camera.RotateX(-mouseMotion.Relative.Y * 0.005f);
            _camera.Rotation = new Vector3(
                Mathf.Clamp(_camera.Rotation.X, -Mathf.Pi/2, Mathf.Pi/2),
                _camera.Rotation.Y,
                _camera.Rotation.Z);
        }

        if (@event.IsActionPressed("ui_cancel"))
        {
            Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured 
                ? Input.MouseModeEnum.Visible 
                : Input.MouseModeEnum.Captured;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsMultiplayerAuthority()) return;

        Vector3 velocity = Velocity;

        // Add gravity
        if (!IsOnFloor())
            velocity.Y -= _gravity * (float)delta;

        // Handle Jump
        if (Input.IsActionJustPressed("ui_accept") && IsOnFloor())
            velocity.Y = JumpVelocity;

        // Get input direction
        Vector2 inputDir = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
        Vector3 direction = (_head.Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();
        
        if (direction != Vector3.Zero)
        {
            velocity.X = direction.X * Speed;
            velocity.Z = direction.Z * Speed;
        }
        else
        {
            velocity.X = Mathf.MoveToward(Velocity.X, 0, Speed);
            velocity.Z = Mathf.MoveToward(Velocity.Z, 0, Speed);
        }

        Velocity = velocity;
        MoveAndSlide();
    }
} 