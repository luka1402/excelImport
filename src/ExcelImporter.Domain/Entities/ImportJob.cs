namespace ExcelImporter.Domain.Entities;

/// <summary>Istorija jednog uvoza (brojači i status).</summary>
public sealed class ImportJob
{
    public long Id { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? FinishedAt { get; set; }

    public required string FileName { get; set; }

    public long FileSize { get; set; }

    public int ConnectionId { get; set; }

    public ConnectionConfig? Connection { get; set; }

    public required string TargetTable { get; set; }

    public int? TemplateId { get; set; }

    public ImportTemplate? Template { get; set; }

    public ImportMode Mode { get; set; }

    public ImportJobStatus Status { get; set; }

    public int TotalRows { get; set; }

    public int ImportedRows { get; set; }

    public int SkippedRows { get; set; }

    public int ErrorRows { get; set; }

    public string? ErrorReportPath { get; set; }

    public List<ImportError> Errors { get; } = [];
}
