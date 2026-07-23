namespace GroundNotes.Services;

public interface ITagFolderCatalogService
{
    Task<IReadOnlyList<string>> LoadAsync(
        string notesFolder,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        string notesFolder,
        IEnumerable<string> folderPaths,
        CancellationToken cancellationToken = default);
}
