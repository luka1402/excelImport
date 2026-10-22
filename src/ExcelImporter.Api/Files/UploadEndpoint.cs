using ExcelImporter.Application.Files;
using ExcelImporter.Infrastructure.Excel;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ExcelImporter.Api.Files;

public static class UploadEndpoint
{
    private const long MaxFileBytes = 50 * 1024 * 1024;
    private const long MaxRequestBytes = MaxFileBytes + 1024 * 1024;

    public static IEndpointRouteBuilder MapUploadEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/files", Upload)
            .WithName("UploadFile")
            .WithMetadata(new RequestSizeLimitAttribute(MaxRequestBytes))
            .Produces<UploadResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);
        return app;
    }

    private static async Task<IResult> Upload(HttpRequest request, IOptions<UploadStorageOptions> options,
        IExcelSheetReader reader, CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return Results.Problem(statusCode: 415, title: "Očekivan je multipart/form-data zahtev.");
        }

        var form = await request.ReadFormAsync(new FormOptions { MultipartBodyLengthLimit = MaxRequestBytes }, cancellationToken);
        var file = form.Files.GetFile("file");
        if (file is null || file.Length == 0)
        {
            return Results.Problem(statusCode: 400, title: "Izaberite neprazan fajl.");
        }
        if (file.Length > MaxFileBytes)
        {
            return Results.Problem(statusCode: 413, title: "Fajl je prevelik (najviše 50 MB).");
        }
        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Problem(statusCode: 415, title: "Dozvoljen je samo .xlsx format.");
        }

        var fileId = Guid.NewGuid();
        var directory = options.Value.ResolveDirectory();
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{fileId:N}.xlsx");
        var keepFile = false;
        try
        {
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                await file.CopyToAsync(output, cancellationToken);
                output.Position = 0;
                reader.ListSheets(output, cancellationToken);
            }
            keepFile = true;
            return Results.Created($"/api/files/{fileId}/analyze", new UploadResponse(fileId));
        }
        catch (UnreadableExcelFileException)
        {
            return Results.Problem(statusCode: 400, title: "Fajl nije ispravan Excel (.xlsx) dokument.");
        }
        finally
        {
            if (!keepFile)
            {
                File.Delete(path);
            }
        }
    }
}

public sealed record UploadResponse(Guid FileId);
