using Godot;

namespace LumiFeast.Game.Boot;

/// <summary>Entry scene. Later it loads settings and opens the first screen.</summary>
public partial class Boot : Node
{
    public override void _Ready() => GD.Print("Lumi's Feast booted.");
}
