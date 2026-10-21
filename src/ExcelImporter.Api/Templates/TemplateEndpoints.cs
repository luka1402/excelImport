using System.Text.Json;
using ExcelImporter.Application.Files;
using ExcelImporter.Application.Mapping;
using ExcelImporter.Application.Templates;
using ExcelImporter.Domain.Rules;

namespace ExcelImporter.Api.Templates;

public static class TemplateEndpoints
{
    public static IEndpointRouteBuilder MapTemplateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/templates");

        group.MapGet("/", List).WithName("ListTemplates").Produces<IReadOnlyList<TemplateSummary>>();
        group.MapGet("/{id:int}", Get).WithName("GetTemplate").Produces<TemplateResponse>().ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPost("/", Create).WithName("CreateTemplate")
            .Produces<TemplateResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);
        group.MapPut("/{id:int}", Update).WithName("UpdateTemplate")
            .Produces<TemplateResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        group.MapDelete("/{id:int}", Delete).WithName("DeleteTemplate").Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        group.MapGet("/{id:int}/apply", Apply).WithName("ApplyTemplate")
            .Produces<ApplyTemplateResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> List(TemplateService service, CancellationToken cancellationToken) =>
        Results.Ok(await service.ListAsync(cancellationToken));

    private static Task<IResult> Get(int id, TemplateService service, CancellationToken cancellationToken) =>
        Handle(async () => Results.Ok(TemplateResponse.From(await service.GetAsync(id, cancellationToken))));

    private static Task<IResult> Create(TemplateRequest request, TemplateService service, CancellationToken cancellationToken) =>
        Handle(async () =>
        {
            var saved = await service.CreateAsync(request.ToDraft(), cancellationToken);
            return Results.Created($"/api/templates/{saved.Id}", TemplateResponse.From(saved));
        });

    private static Task<IResult> Update(int id, TemplateRequest request, TemplateService service, CancellationToken cancellationToken) =>
        Handle(async () => Results.Ok(TemplateResponse.From(await service.UpdateAsync(id, request.ToDraft(), cancellationToken))));

    private static Task<IResult> Delete(int id, TemplateService service, CancellationToken cancellationToken) =>
        Handle(async () =>
        {
            await service.DeleteAsync(id, cancellationToken);
            return Results.NoContent();
        });

    // headerRow je 0-based indeks reda u sheet-u, kao kod analyze; bez njega se header prepoznaje automatski.
    private static Task<IResult> Apply(
        int id, Guid fileId, int? sheet, int? headerRow, TemplateService service, FileAnalyzer analyzer, CancellationToken cancellationToken) =>
        Handle(async () =>
        {
            FileAnalysis? analysis;
            try
            {
                analysis = analyzer.Analyze(fileId, sheet, cancellationToken, headerRow);
            }
            catch (Exception exception) when (exception is UnreadableExcelFileException or InvalidHeaderRowException or SheetNotFoundException)
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: exception.Message);
            }

            if (analysis is null)
            {
                return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Fajl nije pronađen.");
            }

            var fileColumns = analysis.Structure.Columns.Select(column => column.Name).ToList();
            var applied = await service.ApplyAsync(id, fileColumns, cancellationToken);
            return Results.Ok(ApplyTemplateResponse.From(applied));
        });

    private static async Task<IResult> Handle(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (TemplateNotFoundException exception)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: exception.Message);
        }
        catch (DuplicateTemplateNameException exception)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: exception.Message);
        }
        catch (TemplateInvalidException exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Šablon nije ispravan.",
                extensions: new Dictionary<string, object?> { ["errors"] = exception.Errors });
        }
    }
}

public sealed record TemplateRequest(
    string Name,
    int ConnectionId,
    string SchemaName,
    string TableName,
    string? SheetName,
    int HeaderRow,
    IReadOnlyList<string> SourceColumns,
    IReadOnlyList<ColumnMapping> Mappings,
    JsonElement Rules,
    JsonElement Constants)
{
    public TemplateDraft ToDraft()
    {
        try
        {
            var content = TemplateContentSerializer.Parse(
                SourceColumns ?? [],
                Mappings ?? [],
                Rules.ValueKind == JsonValueKind.Undefined ? "[]" : Rules.GetRawText(),
                Constants.ValueKind == JsonValueKind.Undefined ? "[]" : Constants.GetRawText());
            return new TemplateDraft(Name ?? string.Empty, ConnectionId, SchemaName ?? string.Empty, TableName ?? string.Empty, SheetName, HeaderRow, content);
        }
        catch (Exception exception) when (exception is JsonException or RuleSerializationException)
        {
            throw new TemplateInvalidException([exception.Message]);
        }
    }
}

public sealed record TemplateResponse(
    int Id,
    string Name,
    int ConnectionId,
    string SchemaName,
    string TableName,
    string? SheetName,
    int HeaderRow,
    string ColumnsFingerprint,
    IReadOnlyList<string> SourceColumns,
    IReadOnlyList<ColumnMapping> Mappings,
    JsonElement Rules,
    JsonElement Constants,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static TemplateResponse From(SavedTemplate template)
    {
        var (_, rulesJson, constantsJson) = TemplateContentSerializer.Serialize(template.Content);
        return new TemplateResponse(
            template.Id,
            template.Name,
            template.ConnectionId,
            template.SchemaName,
            template.TableName,
            template.SheetName,
            template.HeaderRow,
            template.ColumnsFingerprint,
            template.Content.SourceColumns,
            template.Content.Mappings,
            JsonDocument.Parse(rulesJson).RootElement.Clone(),
            JsonDocument.Parse(constantsJson).RootElement.Clone(),
            template.CreatedAt,
            template.UpdatedAt);
    }
}

public sealed record ApplyTemplateResponse(
    int TemplateId,
    bool IsUsable,
    IReadOnlyList<string> SchemaIssues,
    bool HasColumnWarning,
    IReadOnlyList<string> NewColumns,
    IReadOnlyList<string> MissingColumns,
    IReadOnlyList<ColumnMapping> Mappings,
    JsonElement Rules,
    JsonElement Constants)
{
    public static ApplyTemplateResponse From(TemplateApplication application)
    {
        var template = TemplateResponse.From(application.Template);
        return new ApplyTemplateResponse(
            application.Template.Id,
            application.IsUsable,
            application.SchemaIssues,
            application.HasColumnWarning,
            application.Differences.NewColumns,
            application.Differences.MissingColumns,
            application.Mappings,
            template.Rules,
            template.Constants);
    }
}