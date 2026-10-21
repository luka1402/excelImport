using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ExcelImporter.Api.Errors;

/// <summary>Centralna obrada neobrađenih izuzetaka: loguje se pun izuzetak, klijent dobija samo opšti ProblemDetails.</summary>
public sealed partial class GlobalExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    private const int ClientClosedRequestStatusCode = 499;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            httpContext.Response.StatusCode = ClientClosedRequestStatusCode;
            return true;
        }

        // Neispravno ili prazno telo zahteva je greška klijenta, ne servera.
        if (exception is BadHttpRequestException badRequest)
        {
            httpContext.Response.StatusCode = badRequest.StatusCode;
            return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = new ProblemDetails { Status = badRequest.StatusCode, Title = "Zahtev nije ispravan." },
            });
        }

        LogUnhandled(exception, httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Došlo je do greške na serveru.",
            },
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Neobrađen izuzetak u zahtevu {Method} {Path}.")]
    private partial void LogUnhandled(Exception exception, string method, PathString path);
}
