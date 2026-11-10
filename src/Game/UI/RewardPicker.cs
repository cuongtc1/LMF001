using System.Collections.Generic;
using Godot;
using LumiFeast.Logic.Common;
using LumiFeast.Game.Common;

namespace LumiFeast.Game.UI;

/// <summary>Pick one of three rewards with left, right and attack.</summary>
public partial class RewardPicker : CanvasLayer
{
    private const float CardWidth = 420;
    private const float CardHeight = 380;
    private const float CardIconSize = 128;
    private const float CardGap = 40;
    private const float CardTop = 360;

    private static readonly Color Normal = new(0.12f, 0.1f, 0.2f, 0.88f);
    private static readonly Color Selected = new(0.55f, 0.35f, 0.75f, 0.95f);

    private readonly List<PanelContainer> _cards = [];
    private System.Action<int> _onPick = null!;
    private int _selected;
    private ulong _openedFrame;

    public bool IsOpen => Visible;

    public override void _Ready()
    {
        Layer = 30;
        Visible = false;
    }

    /// <param name="selected">The card selected at first (a shop's "Leave it" so a mashed attack never buys).</param>
    public void Open(IReadOnlyList<(string Title, string Body, Texture2D? Icon)> choices, System.Action<int> onPick, int selected = 0)
    {
        foreach (var card in _cards)
        {
            card.QueueFree();
        }

        _cards.Clear();
        _onPick = onPick;
        _selected = selected;
        var left = (UiCommon.ScreenWidth - choices.Count * CardWidth - (choices.Count - 1) * CardGap) / 2;
        for (var i = 0; i < choices.Count; i++)
        {
            var card = new PanelContainer { Position = new Vector2(left + i * (CardWidth + CardGap), CardTop), CustomMinimumSize = new Vector2(CardWidth, CardHeight) };
            var text = new VBoxContainer();
            if (choices[i].Icon is { } picture)
            {
                text.AddChild(new TextureRect
                {
                    Texture = picture,
                    CustomMinimumSize = Vector2.One * CardIconSize,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                });
            }

            var title = new Label { Text = choices[i].Title, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            title.AddThemeFontSizeOverride("font_size", 34);
            var body = new Label { Text = choices[i].Body, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(CardWidth - 30, 0) };
            body.AddThemeFontSizeOverride("font_size", 24);
            text.AddChild(title);
            text.AddChild(body);
            card.AddChild(text);
            AddChild(card);
            _cards.Add(card);
        }

        Visible = true;
        _openedFrame = Engine.GetProcessFrames();
        Refresh();
    }

    public void Choose(int index)
    {
        Visible = false;
        _onPick(index);
    }

    public override void _Process(double delta)
    {
        // the key that closed the screen before this one must not pick a card too
        if (!Visible || Engine.GetProcessFrames() == _openedFrame)
        {
            return;
        }

        if (Input.IsActionJustPressed(InputActions.MoveLeft))
        {
            _selected = (_selected + _cards.Count - 1) % _cards.Count;
            AudioService.Play(SoundIds.UiMove);
            Refresh();
        }
        else if (Input.IsActionJustPressed(InputActions.MoveRight))
        {
            _selected = (_selected + 1) % _cards.Count;
            AudioService.Play(SoundIds.UiMove);
            Refresh();
        }
        else if (Input.IsActionJustPressed(InputActions.AttackLight))
        {
            AudioService.Play(SoundIds.UiConfirm);
            Choose(_selected);
        }
    }

    private void Refresh()
    {
        for (var i = 0; i < _cards.Count; i++)
        {
            _cards[i].AddThemeStyleboxOverride("panel", UiCommon.RoundPanel(i == _selected ? Selected : Normal, 18));
        }
    }
}
