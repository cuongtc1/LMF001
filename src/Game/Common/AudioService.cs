using System.Collections.Generic;
using Godot;
using LumiFeast.Logic.Common;
using LumiFeast.Logic.Data;

namespace LumiFeast.Game.Common;

/// <summary>
/// Plays sound effects by id from data/audio/sfx.json. A small pool of players lives under the scene root and keeps
/// running while the tree is paused, so menu and result sounds work too.
/// </summary>
public static class AudioService
{
    private const int PoolSize = 16;

    private static readonly Dictionary<string, AudioStream> Streams = [];
    private static readonly RandomNumberGenerator Random = new();
    private static List<AudioStreamPlayer> _pool = [];
    private static int _next;

    public static void Play(string id)
    {
        if (GameServices.Data is not { } data || !EnsurePool())
        {
            return;
        }

        var sound = Find(data.Sounds, id);
        if (sound is null)
        {
            GameLog.Error(MessageKeys.AssetMissing, id);
            return;
        }

        var player = _pool[_next];
        if (!player.IsInsideTree())
        {
            return; // the pool joins the tree at the end of this frame
        }

        _next = (_next + 1) % _pool.Count;
        player.Stream = Stream(data.Sounds.Folder + "/" + sound.Files[Random.RandiRange(0, sound.Files.Count - 1)]);
        player.VolumeDb = sound.VolumeDb;
        player.PitchScale = 1 + Random.RandfRange(-sound.PitchSpread, sound.PitchSpread);
        player.Play();
    }

    private static SoundDef? Find(SoundSetDef set, string id)
    {
        foreach (var sound in set.Sounds)
        {
            if (sound.Id == id)
            {
                return sound;
            }
        }

        return null;
    }

    private static AudioStream Stream(string path)
    {
        if (!Streams.TryGetValue(path, out var stream))
        {
            stream = GD.Load<AudioStream>(GameConstants.ResourceRoot + path);
            Streams[path] = stream;
        }

        return stream;
    }

    /// <summary>Creates the players on first use; false when there is no scene tree yet.</summary>
    private static bool EnsurePool()
    {
        if (_pool.Count > 0 && GodotObject.IsInstanceValid(_pool[0]))
        {
            return true;
        }

        if (Engine.GetMainLoop() is not SceneTree tree)
        {
            return false;
        }

        var holder = new Node { Name = "AudioService", ProcessMode = Node.ProcessModeEnum.Always };
        tree.Root.CallDeferred(Node.MethodName.AddChild, holder);
        _pool = [];
        for (var i = 0; i < PoolSize; i++)
        {
            var player = new AudioStreamPlayer();
            holder.AddChild(player);
            _pool.Add(player);
        }

        _next = 0;
        return true;
    }
}
