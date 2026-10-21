using ExcelImporter.Application.Files;

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

    private static IResult Analyze(Guid fileId, int? sheet, FileAnalyzer analyzer, CancellationToken cancellationToken)
    {
        try
        {
            var analysis = analyzer.Analyze(fileId, sheet, cancellationToken);
            return analysis is null
                ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Fajl nije pronađen.")
                : Results.Ok(AnalyzeResponse.From(analysis));
        }
        catch (UnreadableExcelFileException)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Fajl nije ispravan Excel (.xlsx) dokument.");
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
    bool PreviewColumnsTruncated)
{
    public static AnalyzeResponse From(FileAnalysis analysis) =>
        new(analysis.Sheets, analysis.SelectedSheet.Index, analysis.Preview.Rows, analysis.Preview.ColumnsTruncated);
}
