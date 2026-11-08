using System.Text.Json;
using LumiFeast.Logic.Common;

namespace LumiFeast.Logic.Data;

/// <summary>Loads and validates data/. Throws GameDataException on the first problem.</summary>
public sealed class GameDataLoader(IDataSource source)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = false,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public GameData Load()
    {
        var data = new GameData
        {
            Difficulties = Read<DifficultySetDef>(DataPaths.Difficulty),
            Scaling = Read<ScalingDef>(DataPaths.Scaling),
            Feel = Read<GameFeelDef>(DataPaths.GameFeel),
            Input = Read<InputSetDef>(DataPaths.Input),
            Vfx = Read<VfxSetDef>(DataPaths.CommonVfx),
            Sounds = Read<SoundSetDef>(DataPaths.Sounds),
            Characters = ReadFolder<CharacterDef>(DataPaths.Characters, def => def.Id),
            AttackSets = ReadFolder<AttackSetDef>(DataPaths.Attacks, def => def.Id),
            Monsters = ReadFolder<MonsterDef>(DataPaths.Monsters, def => def.Id),
            Stages = ReadFolder<StageDef>(DataPaths.Stages, def => def.Id),
            Runs = ReadFolder<RunDef>(DataPaths.Runs, def => def.Id),
            SkillSets = ReadFolder<SkillSetDef>(DataPaths.Skills, def => def.Id),
            AmuletSets = ReadFolder<AmuletSetDef>(DataPaths.Amulets, def => def.Id),
        };
        GameDataValidator.Validate(data);
        return data;
    }

    private T Read<T>(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(source.ReadText(path), Options)
                ?? throw new GameDataException(MessageKeys.DataFileInvalid, path, "empty");
        }
        catch (JsonException error)
        {
            throw new GameDataException(MessageKeys.DataFileInvalid, path, error.Message);
        }
    }

    private Dictionary<string, T> ReadFolder<T>(string folder, Func<T, string> idOf)
    {
        var items = new Dictionary<string, T>();
        foreach (var path in source.ListFiles(folder, DataPaths.JsonExtension))
        {
            if (path.EndsWith(DataPaths.SkinFileSuffix, StringComparison.Ordinal))
            {
                continue;
            }

            var item = Read<T>(path);
            if (!items.TryAdd(idOf(item), item))
            {
                throw new GameDataException(MessageKeys.DataDuplicateId, idOf(item), path);
            }
        }

        return items;
    }
}
