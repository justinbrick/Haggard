namespace Haggard.Engine.Content.FileSystem;

/// <summary>
/// Manages content from inside a filesystem.
/// Uses the path of the executing application to determine where to load resources from.
/// </summary>
public sealed class FileSystemContentManager : IContentManager
{
    private readonly string _path;

    public FileSystemContentManager(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentNullException(nameof(path));
        }

        _path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
        if (!Directory.Exists(_path))
        {
            Directory.CreateDirectory(_path);
        }
    }

    public Stream GetContent(string contentPath)
    {
        var toFetch = Path.Combine(_path, contentPath);
        // If the original path is not inside of this path, it means someone has intentionally or accidentally escaped.
        return !toFetch.Contains(_path)
            ? throw new ArgumentOutOfRangeException(nameof(contentPath))
            : File.OpenRead(toFetch);
    }
}
