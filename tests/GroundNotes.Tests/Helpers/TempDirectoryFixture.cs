namespace GroundNotes.Tests.Helpers;

public sealed class TempDirectoryFixture : IDisposable
{
    public string Root { get; } = Path.Combine(
        Path.GetTempPath(), "GroundNotes.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, true);
        }
    }
}
