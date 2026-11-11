using LumiFeast.Logic.Common;

namespace LumiFeast.Logic.Data;

/// <summary>Catches impossible numbers and broken references when the game starts.</summary>
public static class GameDataValidator
{
    public static void Validate(GameData data)
    {
        Require(data.Difficulties.Modes.Count > 0, "difficulty.modes");
        data.Difficulty(data.Difficulties.Default);
        foreach (var mode in data.Difficulties.Modes)
        {
            Require(mode.EnemyHp > 0 && mode.EnemyDamage > 0 && mode.EnemySpeed > 0 && mode.EnemyCooldown > 0, mode.Id);
        }

        Require(data.Scaling.HpGrowthPerLevel >= 1 && data.Scaling.DamageGrowthPerLevel >= 1, "scaling");
        Require(data.Scaling.MinLevel >= 1 && data.Scaling.MaxLevel >= data.Scaling.MinLevel, "scaling.level");
        Require(data.Scaling.Speed.AttackWeight >= 0 && data.Scaling.Speed.MoveWeight >= 0, "scaling.speed");
        Require(data.Scaling.Speed.MaxSpeed >= 1, "scaling.speed.max_speed");

        var sounds = data.Sounds.Sounds.Select(sound => sound.Id).ToList();
        Require(sounds.Count == sounds.Distinct().Count(), "sfx.ids");
        Require(data.Sounds.Sounds.All(sound => sound.Files.Count > 0 && sound.PitchSpread is >= 0 and < 0.5f), "sfx.files");
        foreach (var id in SoundIds.All)
        {
            Require(sounds.Contains(id), $"sfx.{id}");
        }

        foreach (var character in data.Characters.Values)
        {
            Require(character.Stats.MaxHp > 0 && character.Stats.Attack > 0 && character.Stats.Speed > 0, character.Id);
            Require(character.Movement.RunSpeed > 0 && character.Movement.Gravity > 0, character.Id);
            Require(character.Guard.DamageScale is >= 0 and <= 1, character.Id);
            var attacks = data.AttackSet(character.Combat.AttackSet).Attacks.Select(attack => attack.Id).ToHashSet();
            var skills = data.SkillSet(character.Combat.SkillSet);
            var projectiles = skills.Projectiles.Select(projectile => projectile.Id).ToHashSet();
            foreach (var skill in skills.Skills)
            {
                Require(attacks.Contains(skill.Attack), $"{skills.Id}.{skill.Id}");
            }

            foreach (var attack in data.AttackSet(character.Combat.AttackSet).Attacks.Where(attack => attack.Projectile is not null))
            {
                Require(projectiles.Contains(attack.Projectile!), attack.Id);
            }

            foreach (var effect in data.AmuletSets.Values.SelectMany(set => set.Amulets).SelectMany(amulet => amulet.Effects))
            {
                Require(Run.AmuletEffects.All.Contains(effect.Kind), effect.Kind);
                Require(effect.Projectile is null || projectiles.Contains(effect.Projectile), effect.Kind);
                Require(effect.Target is null || attacks.Contains(effect.Target), $"{effect.Kind}.target");
                Require(effect.Kind != Run.AmuletEffects.AttackSlow || (effect.Ticks > 0 && effect.Value is > 0 and < 1), effect.Kind);
            }
        }

        foreach (var set in data.AttackSets.Values)
        {
            var ids = set.Attacks.Select(attack => attack.Id).ToHashSet();
            Require(ids.Contains(set.First), $"{set.Id}.first");
            Require(ids.Contains(set.FirstAir), $"{set.Id}.first_air");
            Require(ids.Contains(set.FirstAirHeavy), $"{set.Id}.first_air_heavy");
            Require(ids.Contains(set.FirstAirUp), $"{set.Id}.first_air_up");
            Require(set.FirstHeavy.Length == 0 || ids.Contains(set.FirstHeavy), $"{set.Id}.first_heavy");
            Require(ids.Contains(set.FirstCounter), $"{set.Id}.first_counter");
            Require(ids.Contains(set.FirstSprint), $"{set.Id}.first_sprint");
            Require(set.FirstAirUpHeavy is null || ids.Contains(set.FirstAirUpHeavy), $"{set.Id}.first_air_up_heavy");
            Require(set.FirstSprintHeavy is null || ids.Contains(set.FirstSprintHeavy), $"{set.Id}.first_sprint_heavy");
            Require(!set.Attacks.Any(attack => attack.PassThrough) || set is { PassReach: > 0, PassMax: > 0 }, $"{set.Id}.pass");
            foreach (var attack in set.Attacks)
            {
                Require(attack.Next is null || ids.Contains(attack.Next), $"{set.Id}.{attack.Id}.next");
                Require(attack.NextHeavy is null || ids.Contains(attack.NextHeavy), $"{set.Id}.{attack.Id}.next_heavy");
                Require(attack.ActiveTicks > 0 && attack.DamageMultiplier > 0, $"{set.Id}.{attack.Id}");
                Require(attack.CancelFromTick <= attack.TotalTicks, $"{set.Id}.{attack.Id}.cancel_from_tick");
                Require(attack.ChainFromTick <= attack.TotalTicks, $"{set.Id}.{attack.Id}.chain_from_tick");
                RequireVfx(data, attack.Vfx);
                RequireVfx(data, attack.HitVfx);
            }
        }

        foreach (var monster in data.Monsters.Values)
        {
            Require(monster.Stats.MaxHp > 0 && monster.Attack.DamageMultiplier >= 0, monster.Id);
            Require(monster.Ai.StunScale >= 0 && monster.Ai.StoicHpMarks.All(mark => mark is > 0 and < 1), monster.Id);
            RequireVfx(data, monster.Attack.HitVfx);
        }

        foreach (var run in data.Runs.Values)
        {
            var rooms = run.Rooms.Select(room => room.Id).ToHashSet();
            Require(rooms.Count == run.Rooms.Count, $"{run.Id}.rooms");
            Require(run.Rooms.All(room => Stages.RoomKinds.All.Contains(room.Kind)), $"{run.Id}.rooms.kind");
            Require(run.Rooms.All(room => room.HasFloor || (room.Down is not null && room.Waves.Count == 0)), $"{run.Id}.rooms.sky");
            foreach (var area in run.Areas ?? [])
            {
                Require(area.Layouts.Count > 0, $"{run.Id}.{area.Id}.layouts");
                Require(area.DoorSprite is null || run.DoorHeight > 0, $"{run.Id}.{area.Id}.door");
                Require(area.Next.All(next => run.Areas!.Any(other => other.Id == next)), $"{run.Id}.{area.Id}.next");
                var gate = area.Next.Count > 0 ? Stages.RoomKinds.Gate : Stages.RoomKinds.Boss;
                Require(area.Layouts.All(layout => layout.Cells.Count(cell => cell.Kind == gate) == 1), $"{run.Id}.{area.Id}.{gate}");
            }

            foreach (var room in run.Rooms.Where(room => room.Kind == Stages.RoomKinds.Gate))
            {
                var doors = run.Areas?.Max(area => area.Next.Count) ?? 0;
                Require(room.Doors is { } list && list.Count >= doors, $"{room.Id}.doors");
            }

            var shop = run.Shop;
            Require(shop.GoldPerMonster >= 0 && shop.SkillPrice > 0 && shop.AmuletPrice > 0 && shop.HealPrice > 0 && shop.KeyPrice > 0 && shop.HealShare is > 0 and <= 1, $"{run.Id}.shop");
            foreach (var room in run.Rooms)
            {
                Require(room.Kind != Stages.RoomKinds.Shop || (room.MerchantX is not null && room.Stands is { Count: > 0 } && room.Stands.Count >= shop.Skills + shop.Amulets + 2), $"{room.Id}.shop");
                Require(room.Height >= RoomDef.ScreenHeight && room.GroundY < room.Height && room.Width >= 1920, $"{room.Id}.size");
                Require(room.Waves.SelectMany(wave => wave.Spawns).All(spawn => spawn.X > 0 && spawn.X < room.Width && (spawn.Y is null || spawn.Y < room.GroundY)), $"{room.Id}.spawns");
            }
            Require(run.Layouts.Count > 0, $"{run.Id}.layouts");
            foreach (var layout in run.Layouts.Concat(run.Areas?.SelectMany(area => area.Layouts) ?? []))
            {
                Require(layout.Cells.All(cell => Stages.RoomKinds.All.Contains(cell.Kind)), $"{layout.Id}.kind");
                Require(layout.Cells.Select(cell => (cell.X, cell.Y)).Distinct().Count() == layout.Cells.Count, $"{layout.Id}.cells");
            }

            // without areas a layout is the whole run; with areas the first area starts it and the last ones end it
            foreach (var layout in run.Areas is null ? run.Layouts : run.Areas[0].Layouts)
            {
                Require(layout.Cells.Count(cell => cell.Kind == Stages.RoomKinds.Entrance) == 1, layout.Id);
                Require(run.Areas is not null || layout.Cells.Count(cell => cell.Kind == Stages.RoomKinds.Boss) == 1, layout.Id);
            }
            data.AmuletSet(run.AmuletSet);
            foreach (var room in run.Rooms.Where(room => room.Challenge is not null))
            {
                var challenge = room.Challenge!;
                Require(Run.ChallengeKinds.All.Contains(challenge.Kind), $"{room.Id}.challenge.kind");
                Require(room.Waves.Count > 0 && challenge.LevelBonus >= 0 && run.Level + challenge.LevelBonus <= data.Scaling.MaxLevel, $"{room.Id}.challenge");
            }

            foreach (var spawn in run.Rooms.SelectMany(room => room.Waves).SelectMany(wave => wave.Spawns))
            {
                data.Monster(spawn.Monster);
            }
        }

        foreach (var stage in data.Stages.Values)
        {
            Require(stage.Level >= data.Scaling.MinLevel && stage.Level <= data.Scaling.MaxLevel, $"{stage.Id}.level");
            Require(stage.Width > 0, $"{stage.Id}.width");
            var waves = stage.Arena?.Waves.SelectMany(wave => wave.Spawns) ?? [];
            foreach (var spawn in stage.Spawns.Concat(waves))
            {
                data.Monster(spawn.Monster);
            }
        }
    }

    private static void RequireVfx(GameData data, string id)
    {
        if (data.Vfx.Effects.All(effect => effect.Id != id))
        {
            throw new GameDataException(MessageKeys.DataUnknownId, id);
        }
    }

    private static void Require(bool condition, string where)
    {
        if (!condition)
        {
            throw new GameDataException(MessageKeys.DataBadValue, where);
        }
    }
}
