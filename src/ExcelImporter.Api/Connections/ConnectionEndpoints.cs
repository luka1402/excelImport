using ExcelImporter.Application.Schema;

namespace ExcelImporter.Api.Connections;

public static class ConnectionEndpoints
{
    public static IEndpointRouteBuilder MapConnectionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/connections");

        group.MapGet("/", List).WithName("ListConnections").Produces<IReadOnlyList<ConnectionSummary>>();
        group.MapGet("/{id:int}/schema", GetSchema).WithName("GetConnectionSchema")
            .Produces<SchemaResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static async Task<IResult> List(IConnectionCatalog catalog, CancellationToken cancellationToken) =>
        Results.Ok(await catalog.ListAsync(cancellationToken));

    private static async Task<IResult> GetSchema(int id, IConnectionCatalog catalog, CancellationToken cancellationToken)
    {
        try
        {
            var schema = await catalog.GetSchemaAsync(id, cancellationToken);
            return Results.Ok(new SchemaResponse(schema.Tables));
        }
        catch (ConnectionNotFoundException exception)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: exception.Message);
        }
        catch (ConnectionUnavailableException exception)
        {
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: exception.Message);
        }
    }
}

public sealed record SchemaResponse(IReadOnlyList<TableSchema> Tables);
