using LumiFeast.Logic.Enemies;
using Xunit;

namespace LumiFeast.Logic.Tests;

public class MonsterPoseTests
{
    private static MonsterPose New(float motion = 1) => new(TestData.Game.Feel.MonsterPose, motion, 0);

    [Fact]
    public void IdleBreathesAroundThePlainPicture()
    {
        var pose = New();
        var heights = Enumerable.Range(0, 200).Select(_ => pose.Step(MonsterState.Idle, 0, 0, true, 0, 1).ScaleY).ToList();
        Assert.True(heights.Max() > 1 && heights.Min() < 1);
        Assert.True(heights.All(height => Math.Abs(height - 1) < 0.05f));
    }

    [Fact]
    public void WindupSquashesMoreAsTheStrikeNears()
    {
        var pose = New();
        var early = pose.Step(MonsterState.Windup, 30, 0, true, 0, 1);
        for (var left = 29; left > 3; left--)
        {
            pose.Step(MonsterState.Windup, left, 0, true, 0, 1);
        }

        var late = pose.Step(MonsterState.Windup, 2, 0, true, 0, 1);
        Assert.True(late.ScaleY < early.ScaleY);
        Assert.True(late.ScaleX > 1);
    }

    [Fact]
    public void TheStrikeStretchesAndLeansForward()
    {
        var right = New().Step(MonsterState.Attack, 10, 0, true, 0, 1);
        var left = New().Step(MonsterState.Attack, 10, 0, true, 0, -1);
        Assert.True(right.ScaleX < 1 && right.ScaleY > 1);
        Assert.True(right.Rotation > 0 && left.Rotation < 0);
    }

    [Fact]
    public void WalkingHops()
    {
        var pose = New();
        var lift = Enumerable.Range(0, 30).Select(_ => pose.Step(MonsterState.Chase, 0, 220, true, 0, 1).OffsetY).Min();
        Assert.True(lift < -5);
    }

    [Fact]
    public void AHitPopsThenSettles()
    {
        var pose = New();
        pose.Step(MonsterState.Stunned, 20, 0, true, 0, 1);
        pose.Hit();
        var pop = pose.Step(MonsterState.Stunned, 19, 0, true, 0, 1);
        Assert.True(pop.ScaleX > 1.1f);
        for (var i = 0; i < 20; i++)
        {
            pose.Step(MonsterState.Stunned, 18, 0, true, 0, 1);
        }

        Assert.Equal(1, pose.Current.ScaleX, 3);
    }

    [Fact]
    public void MotionScalesTheWholeAnimation()
    {
        var stiff = New(0.5f).Step(MonsterState.Attack, 10, 0, true, 0, 1);
        var bouncy = New(1.5f).Step(MonsterState.Attack, 10, 0, true, 0, 1);
        Assert.True(bouncy.ScaleY - 1 > (stiff.ScaleY - 1) * 2.5f);
    }
}
