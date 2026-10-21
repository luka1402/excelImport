using ExcelImporter.Application.Files;
using ExcelImporter.Domain.Columns;

namespace ExcelImporter.Api.Files;

public static class AnalyzeEndpoint
{
    public static IEndpointRouteBuilder MapFileEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/files/{fileId:guid}/analyze", Analyze)
            .WithName("AnalyzeFile")
            .Produces<AnalyzeResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    // headerRow je 0-based indeks reda u sheet-u; bez njega se header prepoznaje automatski.
    private static IResult Analyze(Guid fileId, int? sheet, int? headerRow, FileAnalyzer analyzer, CancellationToken cancellationToken)
    {
        try
        {
            var analysis = analyzer.Analyze(fileId, sheet, cancellationToken, headerRow);
            return analysis is null
                ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Fajl nije pronađen.")
                : Results.Ok(AnalyzeResponse.From(analysis));
        }
        catch (UnreadableExcelFileException)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Fajl nije ispravan Excel (.xlsx) dokument.");
        }
        catch (InvalidHeaderRowException exception)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: exception.Message);
        }
        catch (SheetNotFoundException exception)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: exception.Message);
        }
    }
}

public sealed record AnalyzeResponse(
    IReadOnlyList<SheetInfo> Sheets,
    int SelectedSheetIndex,
    IReadOnlyList<IReadOnlyList<string?>> PreviewRows,
    bool PreviewColumnsTruncated,
    int? HeaderRowIndex,
    bool HeaderChosenManually,
    IReadOnlyList<ColumnProfile> Columns)
{
    public static AnalyzeResponse From(FileAnalysis analysis) =>
        new(
            analysis.Sheets,
            analysis.SelectedSheet.Index,
            analysis.Preview.Rows,
            analysis.Preview.ColumnsTruncated,
            analysis.Structure.HeaderRowIndex,
            analysis.Structure.HeaderChosenManually,
            analysis.Structure.Columns);
}
