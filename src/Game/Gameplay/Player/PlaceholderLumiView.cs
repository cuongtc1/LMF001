using Godot;
using LumiFeast.Game.Common;
using LumiFeast.Logic.Data;

namespace LumiFeast.Game.Gameplay.Player;

/// <summary>Stand-in until the Spine rig exists: Lumi's full picture, a swinging sword, simple squash and lean.</summary>
public partial class PlaceholderLumiView : Node2D, ICharacterView
{
    private const float LeanPerSpeed = 0.00016f;
    private const float StretchPerFallSpeed = 0.00012f;
    private const float MaxStretch = 0.14f;
    private const float DashStretch = 0.22f;
    private const float LandSquash = 0.2f;
    private const float SquashRecovery = 0.18f;
    private const float HurtTilt = -0.3f;
    private const int BlinkTicks = 4;
    private const float GripToCenter = 0.36f;

    private Sprite2D _body = null!;
    private Node2D _swordPivot = null!;
    private PlaceholderViewDef _view = null!;
    private bool _wasOnFloor = true;
    private float _squash;
    private float _swordAngle;
    private int _blink;

    public void Setup(PlaceholderViewDef view)
    {
        _view = view;
        var texture = GameServices.Texture(view.Sprite);
        var bodyScale = view.Height / texture.GetHeight();
        _body = new Sprite2D
        {
            Texture = texture,
            Scale = Vector2.One * bodyScale,
            Position = new Vector2(0, -view.Height / 2),
        };
        AddChild(_body);

        _swordPivot = new Node2D { Position = new Vector2(view.HandX, view.HandY), RotationDegrees = view.SwordRestDegrees };
        var swordTexture = GameServices.Texture(view.SwordSprite);
        var swordScale = view.SwordLength / swordTexture.GetHeight();
        _swordPivot.AddChild(new Sprite2D
        {
            Texture = swordTexture,
            Scale = Vector2.One * swordScale,
            RotationDegrees = 180, // the art has the blade down; the grip must sit in the hand
            Position = new Vector2(0, -view.SwordLength * GripToCenter),
        });
        AddChild(_swordPivot);
        _swordAngle = view.SwordRestDegrees;
    }

    public void Show(in ViewState state)
    {
        if (state.OnFloor && !_wasOnFloor)
        {
            _squash = LandSquash;
        }

        _wasOnFloor = state.OnFloor;
        _squash = Mathf.Lerp(_squash, 0, SquashRecovery);

        var stretch = state.Dashing ? DashStretch : Mathf.Min(MaxStretch, Mathf.Abs(state.VelocityY) * StretchPerFallSpeed);
        var scaleX = state.Dashing ? 1 + stretch : 1 + _squash - stretch;
        var scaleY = state.Dashing ? 1 - stretch : 1 - _squash + stretch;
        Scale = new Vector2(scaleX * state.Facing, scaleY);
        Rotation = state.Hurt ? HurtTilt : state.VelocityX * state.Facing * LeanPerSpeed * state.Facing;

        var target = state.Attacking ? state.SwingDegrees : _view.SwordRestDegrees;
        _swordAngle = state.Attacking ? target : Mathf.Lerp(_swordAngle, target, 0.25f);
        _swordPivot.RotationDegrees = _swordAngle;

        _blink = state.Blinking ? _blink + 1 : 0;
        Visible = !state.Blinking || _blink / BlinkTicks % 2 == 0;
    }
}
