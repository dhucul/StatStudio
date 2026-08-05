using System.Text.Json;
using System.Text.Json.Serialization;

namespace StatStudio.Core.Data;

/// <summary>Serializable snapshot of a worksheet (the StatStudio .ssproj format).</summary>
public sealed class ProjectDto
{
    public string Name { get; set; } = "Worksheet 1";
    public List<ColumnDto> Columns { get; set; } = new();
}

public sealed class ColumnDto
{
    public string Name { get; set; } = "";
    public ColumnType Type { get; set; } = ColumnType.Numeric;
    public List<string?> Cells { get; set; } = new();
}

/// <summary>Reads/writes a worksheet as a JSON .ssproj project file.</summary>
public static class ProjectStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static void Save(Worksheet ws, string path)
    {
        var dto = new ProjectDto { Name = ws.Name };
        foreach (var c in ws.Columns)
            dto.Columns.Add(new ColumnDto { Name = c.Name, Type = c.Type, Cells = c.Cells.ToList() });
        string temporaryPath = AtomicFile.CreateTemporaryPath(path);
        try
        {
            // Stream the document rather than materialising the whole indented JSON as one string,
            // which doubles peak memory and lands a large worksheet on the LOH.
            using (var stream = File.Create(temporaryPath))
                JsonSerializer.Serialize(stream, dto, Options);
            AtomicFile.Commit(temporaryPath, path);
        }
        finally
        {
            AtomicFile.DeleteIfPresent(temporaryPath);
        }
    }

    public static Worksheet Load(string path)
    {
        var dto = JsonSerializer.Deserialize<ProjectDto>(File.ReadAllText(path), Options)
                  ?? throw new InvalidDataException("Project root cannot be null.");
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new InvalidDataException("Project name cannot be empty.");
        if (dto.Columns is null)
            throw new InvalidDataException("Project columns cannot be null.");

        var ws = new Worksheet { Name = dto.Name };
        foreach (var cd in dto.Columns)
        {
            if (cd is null || string.IsNullOrWhiteSpace(cd.Name) || cd.Cells is null)
                throw new InvalidDataException("Project contains an invalid column.");
            var col = ws.AddColumn(cd.Name, cd.Type);
            foreach (var v in cd.Cells) col.Add(v);
        }
        return ws;
    }
}
