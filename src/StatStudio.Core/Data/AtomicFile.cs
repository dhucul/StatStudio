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

    /// <summary>
    /// Best-effort cleanup of the staging file. Callers invoke this from a <c>finally</c>, so a
    /// failure here must never replace the real save error ("the file is open in Excel") with a
    /// misleading one about a .tmp file.
    /// </summary>
    public static void DeleteIfPresent(string temporaryPath)
    {
        try
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
