using Godot;
using LumiFeast.Game.Common;
using LumiFeast.Logic.Data;

namespace LumiFeast.Game.Gameplay.Player;

/// <summary>Lumi drawn by Spine. Attack clips follow the combo's own clock, so art never drifts from the hitbox.</summary>
public partial class SpineLumiView : Node2D, ICharacterView
{
    private const float LeanPerSpeed = 0.00012f;
    private const float StretchPerFallSpeed = 0.00008f;
    private const float MaxStretch = 0.1f;
    private const float DashStretch = 0.1f;
    private const float LandSquash = 0.14f;
    private const float SquashRecovery = 0.18f;
    private const float RunSpeedThreshold = 40;
    private const int BlinkTicks = 4;
    private const int Track = 0;
    private const float MillisecondsPerSecond = 1000;

    private static readonly Color CounterGlow = new(1.5f, 1.3f, 0.6f);

    private SpineViewDef _view = null!;
    private SpineSprite _sprite = null!;
    private SpineSkeletonDataResource _data = null!;
    private SpineTrackEntry? _entry;
    private string _clip = string.Empty;
    private int _swingId = -1;
    private float _clipSeconds;
    private bool _wasOnFloor = true;
    private double _landingEndMs;
    private float _squash;
    private int _blink;

    public void Setup(SpineViewDef view)
    {
        _view = view;
        _data = new SpineSkeletonDataResource();
        _data.Set("skeleton_file_res", GD.Load<Resource>(GameConstants.ResourceRoot + view.Skeleton));
        _data.Set("atlas_res", GD.Load<Resource>(GameConstants.ResourceRoot + view.Atlas));
        _data.Set("default_mix", view.MixSeconds);
        _sprite = new SpineSprite { Scale = Vector2.One * view.Scale };
        _sprite.Set("skeleton_data_res", _data);
        AddChild(_sprite);
        _sprite.GetSkeleton().SetSkinByName(view.Skin);
        _sprite.GetSkeleton().SetSlotsToSetupPose();
        Play(view.Idle, true, 1);
    }

    public void Show(in ViewState state)
    {
        ShowBody(state);
        if (state.Attacking)
        {
            ShowAttack(state);
            return;
        }

        _swingId = -1;
        var clip = PickClip(state);
        if (clip != _clip)
        {
            Play(clip, IsLoop(clip), 1);
        }
    }

    private bool IsLoop(string clip) =>
        clip == _view.Idle || clip == _view.Run || clip == _view.Sprint || clip == _view.Fall || clip == _view.Guard;

    private string PickClip(in ViewState state)
    {
        var speed = Mathf.Abs(state.VelocityX);
        if (state.KnockedDown)
        {
            return _view.Knockdown;
        }

        if (state.GettingUp)
        {
            return _view.Getup;
        }

        if (state.Hurt)
        {
            return _view.Hurt;
        }

        if (state.GuardHit)
        {
            return _view.GuardHit;
        }

        if (state.Guarding)
        {
            return _view.Guard;
        }

        if (state.Dashing)
        {
            return _view.Dash;
        }

        if (!state.OnFloor)
        {
            return state.VelocityY < 0 ? _view.Jump : _view.Fall;
        }

        if (speed > RunSpeedThreshold)
        {
            return speed > _view.SprintFromSpeed ? _view.Sprint : _view.Run;
        }

        return Time.GetTicksMsec() < _landingEndMs ? _view.Land : _view.Idle;
    }

    private void ShowAttack(in ViewState state)
    {
        if (state.SwingId != _swingId)
        {
            _swingId = state.SwingId;
            var clip = _data.FindAnimation(state.AttackId) is null ? _view.DefaultAttack : state.AttackId;
            Play(clip, false, 0);
        }

        _entry?.SetTrackTime(state.AttackProgress * _clipSeconds);
    }

    private void Play(string clip, bool loop, float timeScale)
    {
        _clip = clip;
        _clipSeconds = _data.FindAnimation(clip).GetDuration();
        _entry = _sprite.GetAnimationState().SetAnimation(clip, loop, Track);
        _entry.SetTimeScale(timeScale);
    }

    private void ShowBody(in ViewState state)
    {
        if (state.OnFloor && !_wasOnFloor)
        {
            _squash = LandSquash;
            _landingEndMs = Time.GetTicksMsec() + _data.FindAnimation(_view.Land).GetDuration() * MillisecondsPerSecond;
        }

        _wasOnFloor = state.OnFloor;
        _squash = Mathf.Lerp(_squash, 0, SquashRecovery);
        var stretch = state.Dashing ? DashStretch : Mathf.Min(MaxStretch, Mathf.Abs(state.VelocityY) * StretchPerFallSpeed);
        var scaleX = state.Dashing ? 1 + stretch : 1 + _squash - stretch;
        var scaleY = state.Dashing ? 1 - stretch : 1 - _squash + stretch;
        Scale = new Vector2(scaleX * state.Facing, scaleY);
        Rotation = state.Hurt || state.Attacking ? 0 : Mathf.Abs(state.VelocityX) * LeanPerSpeed;
        Modulate = state.CounterReady ? CounterGlow : Colors.White;
        _blink = state.Blinking ? _blink + 1 : 0;
        Visible = !state.Blinking || _blink / BlinkTicks % 2 == 0;
    }
}
