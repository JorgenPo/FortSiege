using Godot;

public class MobileCamera : Node2D
{

    enum CameraState {
        IDLE,
        DRAG,
        ZOOM
    }

    [Signal]
    public delegate void Moved(Vector2 position);

    public bool Enabled { get; set; }

    private Vector2 dragStartPos;
    private Vector2 cameraDragStartPos;

    private CameraState state;

    private Camera2D camera;

    public override void _Ready()
    {
        Enabled = false;
        camera = GetNode<Camera2D>("Camera2D");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Enabled)
        {
            return;
        }

        if (@event is InputEventScreenTouch touch)
		{
            if (touch.IsPressed())
            {
                GD.Print("Start moving camera");
                //state = CameraState.DRAG;
                dragStartPos = touch.Position;
                cameraDragStartPos = Position;
            }
            else
            {
                GD.Print("Finish moving camera");
            }

            return;
		}

        if (!(@event is InputEventScreenDrag drag))
		{
            return;
		}

        var diff = (drag.Position - dragStartPos);
        var cameraPos = new Vector2(cameraDragStartPos.x - diff.x, cameraDragStartPos.y + diff.y);

        Position = GetPositionInBounds(cameraPos);
        EmitSignal("Moved", Position);
    }

    private Vector2 GetPositionInBounds(Vector2 position)
    {
        var cameraViewportWidth = camera.Zoom.x * GetViewportRect().Size.x;
        var cameraViewportHeight = camera.Zoom.y * GetViewportRect().Size.y;

        if (position.x < 0)
        {
            position.x = 0;
        }
        if (position.x + cameraViewportWidth > GetViewportRect().Size.x)
        {
            position.x = GetViewportRect().Size.x - cameraViewportWidth;
        }
        if (position.y < 0)
        {
            position.y = 0;
        }
        if (position.y + cameraViewportHeight > GetViewportRect().Size.y)
        {
            position.y = GetViewportRect().Size.y - cameraViewportHeight;
        }

        return position;
    }

}
