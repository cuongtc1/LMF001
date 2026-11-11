using System.Linq;
using Godot;
using LumiFeast.Logic.Run;
using LumiFeast.Logic.Data;
using LumiFeast.Game.Common;
using LumiFeast.Game.Gameplay.Player;
using LumiFeast.Logic.Combat;
using LumiFeast.Logic.Common;

namespace LumiFeast.Game.UI;

/// <summary>HP, MP, combo counter and the arena wave.</summary>
public partial class PrototypeHud : CanvasLayer
{
    private const float StatusLeft = 480;
    private const float StatusTop = 150;

    private PlayerController _player = null!;
    private ProgressBar _hp = null!;
    private ProgressBar _mp = null!;
    private Label _combo = null!;
    private Label _status = null!;
    private ProgressBar _bossBar = null!;
    private Health? _boss;
    private const float SkillIconSize = 64;
    private const float AmuletIconSize = 44;

    private readonly System.Collections.Generic.List<Label> _skills = [];
    private readonly System.Collections.Generic.List<TextureRect> _skillIcons = [];
    private readonly System.Collections.Generic.List<string?> _shownSkills = [];
    private HBoxContainer _amuletRow = null!;
    private Label? _steamDots;
    private int _shownAmulets = -1;

    /// <summary>Keys carried, shown next to a key icon; null hides the counter (outside a run).</summary>
    public System.Func<int>? Keys { get; set; }

    public string KeySprite { get; set; } = string.Empty;

    /// <summary>Gold carried; null hides it (outside a run).</summary>
    public System.Func<int>? Gold { get; set; }

    private Label? _goldCount;


    /// <summary>Coin drawn before the gold count.</summary>
    public string GoldSprite { get; set; } = string.Empty;

    private Label? _keyCount;

    /// <summary>The run's amulet set, for icons; empty outside a run.</summary>
    public string AmuletSetId { get; set; } = string.Empty;

    public void Setup(PlayerController player)
    {
        _player = player;
        Layer = 10;
        var column = new VBoxContainer { Position = new Vector2(32, 28), CustomMinimumSize = new Vector2(340, 0) };
        AddChild(column);
        _hp = AddBar(column, new Color("e2506a"), player.Health.Max);
        _mp = AddBar(column, new Color("6c8cf5"), player.MaxMp);
        column.AddChild(new Label { Text = GameServices.Messages.Get(MessageKeys.DebugHelp) });
        var row = new HBoxContainer();
        column.AddChild(row);
        for (var i = 0; i < player.Loadout.Slots.Count; i++)
        {
            var slot = new HBoxContainer { CustomMinimumSize = new Vector2(200, SkillIconSize) };
            var iconRect = new TextureRect
            {
                CustomMinimumSize = Vector2.One * SkillIconSize,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            };
            var label = UiCommon.OutlinedLabel(24);
            label.VerticalAlignment = VerticalAlignment.Center;
            slot.AddChild(iconRect);
            slot.AddChild(label);
            row.AddChild(slot);
            _skills.Add(label);
            _skillIcons.Add(iconRect);
            _shownSkills.Add(null);
        }

        if (Keys is not null)
        {
            var keyRow = new HBoxContainer();
            keyRow.AddChild(new TextureRect
            {
                Texture = GameServices.Texture(KeySprite),
                CustomMinimumSize = Vector2.One * AmuletIconSize,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            });
            _keyCount = UiCommon.OutlinedLabel(28);
            _keyCount.VerticalAlignment = VerticalAlignment.Center;
            keyRow.AddChild(_keyCount);
            _goldCount = UiCommon.OutlinedLabel(28);
            _goldCount.VerticalAlignment = VerticalAlignment.Center;
            _goldCount.AddThemeColorOverride("font_color", new Color("ffd34d"));
            keyRow.AddChild(new Control { CustomMinimumSize = new Vector2(24, 0) });
            if (GoldSprite.Length > 0)
            {
                keyRow.AddChild(new TextureRect
                {
                    Texture = GameServices.Texture(GoldSprite),
                    CustomMinimumSize = Vector2.One * AmuletIconSize * 0.8f,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                });
            }

            keyRow.AddChild(_goldCount);
            column.AddChild(keyRow);
        }

        _amuletRow = new HBoxContainer();
        column.AddChild(_amuletRow);

        _combo = UiCommon.OutlinedLabel(48);
        _combo.Position = new Vector2(1600, 300);
        _combo.Visible = false;
        AddChild(_combo);

        _bossBar = new ProgressBar { ShowPercentage = false, Position = new Vector2(460, 980), CustomMinimumSize = new Vector2(1000, 24), Visible = false };
        _bossBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color("b45cc9") });
        AddChild(_bossBar);

        // centered under the bars, clear of the minimap in the top-right corner
        _status = UiCommon.OutlinedLabel(38);
        _status.Position = new Vector2(StatusLeft, StatusTop);
        _status.Size = new Vector2(UiCommon.ScreenWidth - StatusLeft * 2, 0);
        _status.HorizontalAlignment = HorizontalAlignment.Center;
        _status.Visible = false;
        AddChild(_status);
    }

    /// <summary>
    /// A chest's reward: icon, name and its one-line description for a few seconds (round 73), so the run never stops
    /// for a choice card.
    /// </summary>
    public void ShowReward(Texture2D? icon, string title, string line)
    {
        if (_rewardCard is not null && IsInstanceValid(_rewardCard))
        {
            _rewardCard.QueueFree(); // the last card may have faded out and freed itself already
        }
        var card = new PanelContainer { Position = new Vector2(RewardLeft, RewardTop), CustomMinimumSize = new Vector2(UiCommon.ScreenWidth - RewardLeft * 2, 0) };
        card.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.1f, 0.08f, 0.15f, 0.82f), CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14, CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14, ContentMarginLeft = 18, ContentMarginRight = 18, ContentMarginTop = 10, ContentMarginBottom = 10 });
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 18);
        card.AddChild(row);
        if (icon is not null)
        {
            row.AddChild(new TextureRect { Texture = icon, CustomMinimumSize = Vector2.One * RewardIconSize, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
        }

        var text = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddChild(text);
        var name = UiCommon.OutlinedLabel(34, title);
        name.AddThemeColorOverride("font_color", new Color("ffd34d"));
        text.AddChild(name);
        text.AddChild(UiCommon.OutlinedLabel(24, line));
        AddChild(card);
        _rewardCard = card;
        var tween = card.CreateTween();
        tween.TweenInterval(RewardSeconds);
        tween.TweenProperty(card, "modulate:a", 0f, 0.4f);
        tween.TweenCallback(Callable.From(card.QueueFree));
    }

    private Control? _rewardCard;
    private const float RewardLeft = 560;
    private const float RewardTop = 200;
    private const float RewardIconSize = 96;
    private const float RewardSeconds = 3.2f;

    /// <summary>Shows a boss health bar; null hides it.</summary>
    public void TrackBoss(Health? boss)
    {
        _boss = boss;
        _bossBar.Visible = boss is not null;
        if (boss is not null)
        {
            _bossBar.MaxValue = boss.Max;
        }
    }

    public void ShowStatus(string text)
    {
        _status.Text = text;
        _status.Visible = text.Length > 0;
    }

    public override void _Process(double delta)
    {
        _hp.MaxValue = _player.Health.Max;
        if (_keyCount is not null && Keys is not null)
        {
            _keyCount.Text = "x" + Keys();
        }

        if (_goldCount is not null && Gold is not null)
        {
            _goldCount.Text = GameServices.Messages.Get(MessageKeys.HudGold, Gold());
        }
        _hp.Value = _player.Health.Current;
        _mp.Value = _player.Mp;
        _bossBar.Value = _boss?.Current ?? 0;
        ShowSkills();
        _combo.Visible = _player.ComboHits > 1;
        _combo.Text = GameServices.Messages.Get(MessageKeys.HudCombo, _player.ComboHits);
    }

    private void ShowSkills()
    {
        var messages = GameServices.Messages;
        for (var i = 0; i < _skills.Count; i++)
        {
            var key = InputSetup.FirstKey(InputActions.Skills[i]);
            var slotSkill = _player.Loadout.Slots[i];
            if (_shownSkills[i] != slotSkill?.Id)
            {
                _shownSkills[i] = slotSkill?.Id;
                _skillIcons[i].Texture = slotSkill is null ? null : GameIcons.Skill(_player.SkillSetId, slotSkill.Id);
            }

            if (slotSkill is not { } skill)
            {
                _skills[i].Text = messages.Get(MessageKeys.HudSkillEmpty, key);
                continue;
            }

            var cooldown = _player.Loadout.CooldownLeft(i);
            var state = cooldown > 0 ? messages.Get(MessageKeys.HudSkillCooldown, Mathf.CeilToInt(cooldown / GameConstants.TicksPerSecond)) : messages.Get(MessageKeys.HudSkillReady);
            var level = _player.Loadout.Level(i);
            _skills[i].Text = messages.Get(MessageKeys.HudSkillSlot, key, level > 1 ? "Lv" + level : string.Empty, state);
            var dim = cooldown > 0 || _player.Mp < skill.MpCost ? Colors.Gray : Colors.White;
            _skills[i].Modulate = dim;
            _skillIcons[i].Modulate = dim;
        }

        ShowAmulets();
    }

    /// <summary>One small icon per amulet; Steam Bath also shows its counters left in this room as dots.</summary>
    private void ShowAmulets()
    {
        var amulets = _player.Modifiers.Amulets;
        if (amulets.Count != _shownAmulets)
        {
            _shownAmulets = amulets.Count;
            foreach (var child in _amuletRow.GetChildren())
            {
                child.QueueFree();
            }

            _steamDots = null;
            foreach (var amulet in amulets)
            {
                _amuletRow.AddChild(new TextureRect
                {
                    Texture = AmuletSetId.Length > 0 ? GameIcons.Amulet(AmuletSetId, amulet.Id) : null,
                    CustomMinimumSize = Vector2.One * AmuletIconSize,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                });
                if (amulet.Effects.Any(effect => effect.Kind == AmuletEffects.AutoCounter))
                {
                    _steamDots = UiCommon.OutlinedLabel(22);
                    _steamDots.VerticalAlignment = VerticalAlignment.Center;
                    _amuletRow.AddChild(_steamDots);
                }
            }
        }

        if (_steamDots is not null)
        {
            var limit = Mathf.RoundToInt(_player.Modifiers.Total(AmuletEffects.AutoCounter));
            var left = _player.SteamCountersLeft;
            _steamDots.Text = new string('●', left) + new string('○', Mathf.Max(0, limit - left));
        }
    }

    private static ProgressBar AddBar(Container parent, Color color, double max)
    {
        var bar = new ProgressBar { MaxValue = max, ShowPercentage = false, CustomMinimumSize = new Vector2(340, 22) };
        bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = color });
        parent.AddChild(bar);
        return bar;
    }
}
