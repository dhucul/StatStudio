namespace StatStudio.Core.Data;

internal static class AtomicFile
{
    public static string CreateTemporaryPath(string destination)
    {
        string fullPath = Path.GetFullPath(destination);
        string directory = Path.GetDirectoryName(fullPath)
                           ?? throw new ArgumentException("Destination must have a parent directory.", nameof(destination));
        string extension = Path.GetExtension(fullPath);
        string fileName = Path.GetFileNameWithoutExtension(fullPath);
        return Path.Combine(directory, $".{fileName}.{Guid.NewGuid():N}.tmp{extension}");
    }

    public static void Commit(string temporaryPath, string destination)
    {
        string fullDestination = Path.GetFullPath(destination);
        if (File.Exists(fullDestination))
            File.Move(temporaryPath, fullDestination, overwrite: true);
        else
            File.Move(temporaryPath, fullDestination);
    }

    public static void DeleteIfPresent(string temporaryPath)
    {
        if (File.Exists(temporaryPath))
            File.Delete(temporaryPath);
    }
}
