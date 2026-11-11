using System.Collections.Generic;
using System.Linq;
using Godot;
using LumiFeast.Game.Common;
using LumiFeast.Logic.Common;
using LumiFeast.Logic.Data;

namespace LumiFeast.Game.UI;

/// <summary>
/// The entrance shrine's screen (H2, round 75): every owned skill in a row; left and right move, Z equips or removes,
/// X closes. At most <c>slots</c> skills are equipped; the run only levels up the equipped ones.
/// </summary>
public partial class SkillLoadoutScreen : CanvasLayer
{
    private const float CardWidth = 270;
    private const float CardHeight = 330;
    private const float CardGap = 24;
    private const float CardTop = 330;
    private const float IconSize = 112;
    private static readonly Color Normal = new(0.12f, 0.1f, 0.2f, 0.88f);
    private static readonly Color Selected = new(0.55f, 0.35f, 0.75f, 0.95f);
    private static readonly Color EquippedColor = new("ffd34d");

    private readonly List<PanelContainer> _cards = [];
    private readonly List<Label> _marks = [];
    private IReadOnlyList<SkillDef> _skills = [];
    private List<string> _equipped = [];
    private int _slots;
    private int _selected;
    private ulong _openedFrame;
    private Label _hint = null!;
    private System.Action<IReadOnlyList<string>> _onClose = null!;

    public override void _Ready()
    {
        Layer = 30;
        Visible = false;
    }

    public void Open(SkillSetDef set, IReadOnlyList<string> equipped, System.Action<IReadOnlyList<string>> onClose)
    {
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        _cards.Clear();
        _marks.Clear();
        _skills = set.Skills;
        _slots = set.Slots;
        _equipped = [.. equipped];
        _onClose = onClose;
        _selected = 0;
        var messages = GameServices.Messages;
        var title = UiCommon.OutlinedLabel(44, messages.Get(MessageKeys.LoadoutTitle, _slots));
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.Size = new Vector2(UiCommon.ScreenWidth, 0);
        title.Position = new Vector2(0, CardTop - 120);
        AddChild(title);
        var left = (UiCommon.ScreenWidth - _skills.Count * CardWidth - (_skills.Count - 1) * CardGap) / 2;
        for (var i = 0; i < _skills.Count; i++)
        {
            var skill = _skills[i];
            var card = new PanelContainer { Position = new Vector2(left + i * (CardWidth + CardGap), CardTop), CustomMinimumSize = new Vector2(CardWidth, CardHeight) };
            var column = new VBoxContainer();
            column.AddChild(new TextureRect
            {
                Texture = GameIcons.Skill(set.Id, skill.Id),
                CustomMinimumSize = Vector2.One * IconSize,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            });
            var name = new Label { Text = messages.Get(MessageKeys.SkillPrefix + skill.Id), AutowrapMode = TextServer.AutowrapMode.WordSmart, HorizontalAlignment = HorizontalAlignment.Center };
            name.AddThemeFontSizeOverride("font_size", 28);
            column.AddChild(name);
            var body = new Label
            {
                Text = messages.Get(MessageKeys.SkillPrefix + skill.Id + MessageKeys.DescriptionSuffix) + "\n" + messages.Get(MessageKeys.RewardSkill, skill.MpCost),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(CardWidth - 24, 0),
            };
            body.AddThemeFontSizeOverride("font_size", 19);
            column.AddChild(body);
            var mark = UiCommon.OutlinedLabel(26);
            mark.HorizontalAlignment = HorizontalAlignment.Center;
            mark.AddThemeColorOverride("font_color", EquippedColor);
            column.AddChild(mark);
            card.AddChild(column);
            AddChild(card);
            _cards.Add(card);
            _marks.Add(mark);
        }

        _hint = UiCommon.OutlinedLabel(28);
        _hint.HorizontalAlignment = HorizontalAlignment.Center;
        _hint.Size = new Vector2(UiCommon.ScreenWidth, 0);
        _hint.Position = new Vector2(0, CardTop + CardHeight + 60);
        AddChild(_hint);
        Visible = true;
        _openedFrame = Engine.GetProcessFrames();
        Refresh();
    }

    public override void _Process(double delta)
    {
        // the hit that opened the screen must not toggle a card too
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
            Toggle(_skills[_selected].Id);
        }
        else if (Input.IsActionJustPressed(InputActions.AttackHeavy))
        {
            Close();
        }
    }

    public void Close()
    {
        AudioService.Play(SoundIds.UiBack);
        Visible = false;
        _onClose(_skills.Select(skill => skill.Id).Where(_equipped.Contains).ToList());
    }

    private void Toggle(string id)
    {
        if (_equipped.Remove(id))
        {
            AudioService.Play(SoundIds.UiBack);
        }
        else if (_equipped.Count < _slots)
        {
            _equipped.Add(id);
            AudioService.Play(SoundIds.UiConfirm);
        }
        else
        {
            AudioService.Play(SoundIds.Hurt);
        }

        Refresh();
    }

    private void Refresh()
    {
        var messages = GameServices.Messages;
        for (var i = 0; i < _cards.Count; i++)
        {
            var on = _equipped.Contains(_skills[i].Id);
            _cards[i].AddThemeStyleboxOverride("panel", UiCommon.RoundPanel(i == _selected ? Selected : Normal, 18));
            _marks[i].Text = on ? messages.Get(MessageKeys.LoadoutEquipped) : string.Empty;
        }

        _hint.Text = messages.Get(MessageKeys.LoadoutHint, _equipped.Count, _slots);
    }
}
