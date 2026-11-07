using System.Linq;
using Godot;
using LumiFeast.Logic.Data;

namespace LumiFeast.Game.Common;

/// <summary>Builds the input map from data/input, which is also where remapping will save to.</summary>
public static class InputSetup
{
    /// <summary>The first keyboard key of an action, for on-screen hints.</summary>
    public static string FirstKey(string action) =>
        GameServices.Data.Input.Actions.FirstOrDefault(item => item.Action == action)?.Keys.FirstOrDefault() ?? string.Empty;

    public static void Apply(InputSetDef input)
    {
        foreach (var action in input.Actions)
        {
            if (InputMap.HasAction(action.Action))
            {
                InputMap.EraseAction(action.Action);
            }

            InputMap.AddAction(action.Action, input.PadDeadzone);
            foreach (var key in action.Keys)
            {
                InputMap.ActionAddEvent(action.Action, new InputEventKey { PhysicalKeycode = OS.FindKeycodeFromString(key) });
            }

            foreach (var button in action.PadButtons)
            {
                InputMap.ActionAddEvent(action.Action, new InputEventJoypadButton { ButtonIndex = (JoyButton)button });
            }

            foreach (var direction in action.PadAxes)
            {
                InputMap.ActionAddEvent(action.Action, new InputEventJoypadMotion { Axis = JoyAxis.LeftX, AxisValue = direction });
            }
        }
    }
}
