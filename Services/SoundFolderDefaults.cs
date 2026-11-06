namespace TapOS.Services;

public static class SoundFolderDefaults
{
    public static string GetDefaultPath()
    {
        var candidates = new List<string>();
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 4 && directory is not null; i++, directory = directory.Parent)
        {
            candidates.Add(Path.Combine(directory.FullName, "sounds"));
        }

        return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
    }

    public static bool IsBuildOutputPath(string path)
    {
        var normalized = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        return normalized.EndsWith($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}Debug{Path.DirectorySeparatorChar}net8.0-windows{Path.DirectorySeparatorChar}sounds", StringComparison.OrdinalIgnoreCase) ||
               normalized.EndsWith($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}net8.0-windows{Path.DirectorySeparatorChar}sounds", StringComparison.OrdinalIgnoreCase);
    }
}
