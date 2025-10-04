namespace Haggard.Engine.Content;

/// <summary>
/// A manager that handles content required for an engine or game to properly function.
/// </summary>
public interface IContentManager
{
    /// <summary>
    /// Retrieves content from a specific path.
    /// </summary>
    /// <param name="contentPath">a path</param>
    /// <returns>a stream to the data that is being stored at the path.</returns>
    public Stream GetContent(string contentPath);
}
