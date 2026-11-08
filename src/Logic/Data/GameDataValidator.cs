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

        Require(data.Scaling.HpGrowthPerTier > 0 && data.Scaling.DamageGrowthPerTier > 0, "scaling");
        Require(data.Scaling.MinTier >= 1 && data.Scaling.MaxTier >= data.Scaling.MinTier, "scaling.tier");
        Require(data.Scaling.Speed.AttackWeight >= 0 && data.Scaling.Speed.MoveWeight >= 0, "scaling.speed");
        Require(data.Scaling.Speed.MaxSpeed >= 1, "scaling.speed.max_speed");

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
            }
        }

        foreach (var set in data.AttackSets.Values)
        {
            var ids = set.Attacks.Select(attack => attack.Id).ToHashSet();
            Require(ids.Contains(set.First), $"{set.Id}.first");
            Require(ids.Contains(set.FirstAir), $"{set.Id}.first_air");
            Require(ids.Contains(set.FirstAirHeavy), $"{set.Id}.first_air_heavy");
            Require(ids.Contains(set.FirstAirUp), $"{set.Id}.first_air_up");
            Require(ids.Contains(set.FirstHeavy), $"{set.Id}.first_heavy");
            Require(ids.Contains(set.FirstCounter), $"{set.Id}.first_counter");
            Require(ids.Contains(set.FirstSprint), $"{set.Id}.first_sprint");
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
            Require(run.Layouts.Count > 0, $"{run.Id}.layouts");
            foreach (var layout in run.Layouts)
            {
                Require(layout.Cells.All(cell => Stages.RoomKinds.All.Contains(cell.Kind)), $"{layout.Id}.kind");
                Require(layout.Cells.Count(cell => cell.Kind == Stages.RoomKinds.Entrance) == 1 && layout.Cells.Count(cell => cell.Kind == Stages.RoomKinds.Boss) == 1, layout.Id);
                Require(layout.Cells.Select(cell => (cell.X, cell.Y)).Distinct().Count() == layout.Cells.Count, $"{layout.Id}.cells");
            }
            data.AmuletSet(run.AmuletSet);
            foreach (var room in run.Rooms.Where(room => room.Challenge is not null))
            {
                var challenge = room.Challenge!;
                Require(Run.ChallengeKinds.All.Contains(challenge.Kind), $"{room.Id}.challenge.kind");
                Require(room.Waves.Count > 0 && challenge.TierBonus >= 0 && run.Tier + challenge.TierBonus <= data.Scaling.MaxTier, $"{room.Id}.challenge");
            }

            foreach (var spawn in run.Rooms.SelectMany(room => room.Waves).SelectMany(wave => wave.Spawns))
            {
                data.Monster(spawn.Monster);
            }
        }

        foreach (var stage in data.Stages.Values)
        {
            Require(stage.Tier >= data.Scaling.MinTier && stage.Tier <= data.Scaling.MaxTier, $"{stage.Id}.tier");
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
