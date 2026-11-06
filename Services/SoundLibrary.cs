using System.Media;

namespace TapOS.Services;

public sealed class SoundLibrary
{
    private readonly object _sync = new();
    private readonly Random _random;
    private readonly List<SoundEntry> _sounds = [];
    private int _lastIndex = -1;

    public SoundLibrary(Random? random = null)
    {
        _random = random ?? Random.Shared;
    }

    public IReadOnlyList<string> SoundPaths
    {
        get
        {
            lock (_sync)
            {
                return _sounds.Select(sound => sound.Path).ToArray();
            }
        }
    }

    public string? LastError { get; private set; }

    public int Reload(string folder)
    {
        lock (_sync)
        {
            _sounds.Clear();
            _lastIndex = -1;
            LastError = null;

            if (!Directory.Exists(folder))
            {
                LastError = $"Sound folder does not exist: {folder}";
                return 0;
            }

            foreach (var path in Directory.EnumerateFiles(folder, "*.wav", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var player = new SoundPlayer(path);
                    player.Load();
                    _sounds.Add(new SoundEntry(path, player));
                }
                catch (Exception ex)
                {
                    LastError = $"Could not load {Path.GetFileName(path)}: {ex.Message}";
                }
            }

            return _sounds.Count;
        }
    }

    public bool TryPlayRandom(out string? path, out string? error)
    {
        lock (_sync)
        {
            path = null;
            error = null;
            if (_sounds.Count == 0)
            {
                error = LastError ?? "No playable .wav files are loaded.";
                return false;
            }

            var index = _sounds.Count == 1
                ? 0
                : NextIndexExcludingLast(_sounds.Count);
            _lastIndex = index;
            var sound = _sounds[index];
            path = sound.Path;
            try
            {
                sound.Player.Play();
                return true;
            }
            catch (Exception ex)
            {
                error = $"Could not play {Path.GetFileName(path)}: {ex.Message}";
                return false;
            }
        }
    }

    private int NextIndexExcludingLast(int count)
    {
        var index = _random.Next(count - 1);
        if (index >= _lastIndex)
        {
            index++;
        }

        return index;
    }

    private sealed record SoundEntry(string Path, SoundPlayer Player);
}
