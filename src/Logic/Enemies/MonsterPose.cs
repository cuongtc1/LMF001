using LumiFeast.Logic.Data;

namespace LumiFeast.Logic.Enemies;

/// <summary>Offsets applied on top of a monster's still image. Scale 1 and offset 0 mean the plain picture.</summary>
public readonly record struct Pose(float OffsetX, float OffsetY, float ScaleX, float ScaleY, float Rotation)
{
    public static readonly Pose Rest = new(0, 0, 1, 1, 0);
}

/// <summary>
/// Simple code-driven animation for prototype monsters: breathing, hops while walking, a squash and shake
/// before an attack, a stretch on the strike, a pop when hit, squash on landing. Works on any image,
/// so new monster designs need no rig until they are final.
/// </summary>
public sealed class MonsterPose(MonsterPoseDef def, float motion, int phaseTicks)
{
    private MonsterState _state = MonsterState.Idle;
    private int _stateLength = 1;
    private int _age = phaseTicks;
    private int _sinceHit = int.MaxValue;
    private int _sinceLanding = int.MaxValue;
    private bool _wasOnFloor = true;

    public Pose Current { get; private set; } = Pose.Rest;

    public void Hit() => _sinceHit = 0;

    /// <param name="stateTicksLeft">Ticks left in the brain's current state, used for the wind-up build-up.</param>
    /// <param name="speedX">Walking speed in pixels per second, signed.</param>
    /// <param name="facing">1 right, -1 left.</param>
    public Pose Step(MonsterState state, int stateTicksLeft, float speedX, bool onFloor, float velocityY, int facing)
    {
        if (state != _state)
        {
            _state = state;
            _stateLength = Math.Max(1, stateTicksLeft);
        }

        if (onFloor && !_wasOnFloor)
        {
            _sinceLanding = 0;
        }

        _wasOnFloor = onFloor;
        _age++;
        Current = Compute(state, stateTicksLeft, speedX, onFloor, velocityY, facing);
        _sinceHit = Bump(_sinceHit);
        _sinceLanding = Bump(_sinceLanding);
        return Current;
    }

    private static int Bump(int ticks) => ticks == int.MaxValue ? ticks : ticks + 1;

    private Pose Compute(MonsterState state, int stateTicksLeft, float speedX, bool onFloor, float velocityY, int facing)
    {
        if (state == MonsterState.Down)
        {
            return Pose.Rest; // lying down is drawn by the controller
        }

        float squash = 0; // positive: wider and shorter
        float offsetX = 0;
        float offsetY = 0;
        float rotation = 0;
        var walking = Math.Clamp(Math.Abs(speedX) / def.StepSpeed, 0, 1);

        switch (state)
        {
            case MonsterState.Windup:
                var build = 1 - (float)stateTicksLeft / _stateLength;
                squash += def.WindupSquash * build;
                offsetX += MathF.Sin(_age * def.ShakeSpeed) * def.WindupShake * build;
                rotation -= def.Lean * build * facing; // leans back before springing
                break;
            case MonsterState.Attack:
                squash -= def.AttackStretch;
                rotation += def.Lean * facing;
                break;
            case MonsterState.Stunned:
                break; // frozen in the hit picture; only the hit pop moves it
            case MonsterState.Dizzy:
                rotation += MathF.Sin(_age * def.ShakeSpeed * 0.15f) * def.DizzyWobble;
                break;
            default:
                if (onFloor && walking > 0)
                {
                    var step = MathF.Abs(MathF.Sin(MathF.PI * _age / def.StepTicks));
                    offsetY -= step * def.StepHop * walking;
                    squash += (1 - step) * def.StepSquash * walking;
                    rotation += def.Lean * walking * facing;
                }
                else
                {
                    squash += MathF.Sin(MathF.Tau * _age / def.BreatheTicks) * def.Breathe;
                }

                break;
        }

        if (!onFloor)
        {
            squash -= def.AirStretch * Math.Clamp(-velocityY / def.StepSpeed, -1, 1);
        }

        if (_sinceLanding < def.LandTicks)
        {
            squash += def.LandSquash * (1 - (float)_sinceLanding / def.LandTicks);
        }

        if (_sinceHit < def.HitTicks)
        {
            squash += def.HitSquash * (1 - (float)_sinceHit / def.HitTicks);
        }

        squash *= motion;
        return new Pose(offsetX * motion, offsetY * motion, 1 + squash, 1 - squash, rotation * motion);
    }
}
