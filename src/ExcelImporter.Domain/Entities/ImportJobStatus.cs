namespace ExcelImporter.Domain.Entities;

public enum ImportJobStatus
{
    Running,
    Completed,
    CompletedWithErrors,
    Failed,
    Cancelled,
}
