using Defra.WasteObligations.Api.Data;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Utils.ErrorHandling;

public static class ApplicationBuilderExtensions
{
    public static void UseErrorHandling(this IApplicationBuilder app)
    {
        app.UseExceptionHandler(
            new ExceptionHandlerOptions
            {
                AllowStatusCode404Response = true,
                ExceptionHandler = async context =>
                {
                    var exceptionHandlerFeature = context.Features.Get<IExceptionHandlerFeature>();
                    var error = exceptionHandlerFeature?.Error;
                    var (statusCode, title, detail) = error switch
                    {
                        BadHttpRequestException ex => (ex.StatusCode, "Bad request", ex.Message),
                        EntityException ex => (
                            StatusCodes.Status422UnprocessableEntity,
                            "Entity state conflict",
                            ex.Message
                        ),
                        ConcurrencyException ex => (StatusCodes.Status409Conflict, "Concurrency conflict", ex.Message),
                        _ => (
                            StatusCodes.Status500InternalServerError,
                            "An error occurred while processing your request.",
                            null
                        ),
                    };

                    context.Response.StatusCode = statusCode;

                    var problemDetails = new ProblemDetails
                    {
                        Title = title,
                        Detail = detail,
                        Status = statusCode,
                    };

                    await context
                        .RequestServices.GetRequiredService<IProblemDetailsService>()
                        .WriteAsync(
                            new ProblemDetailsContext
                            {
                                HttpContext = context,
                                AdditionalMetadata = exceptionHandlerFeature?.Endpoint?.Metadata,
                                ProblemDetails = problemDetails,
                            }
                        );
                },
            }
        );
    }
}
