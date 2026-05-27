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
                    var problemDetails = new ProblemDetails();

                    switch (error)
                    {
                        case BadHttpRequestException badHttpRequestException:
                            context.Response.StatusCode = badHttpRequestException.StatusCode;
                            problemDetails.Title = "Bad request";
                            problemDetails.Detail = badHttpRequestException.Message;
                            problemDetails.Status = badHttpRequestException.StatusCode;
                            break;

                        case EntityException entityException:
                            context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
                            problemDetails.Title = "Entity state conflict";
                            problemDetails.Detail = entityException.Message;
                            problemDetails.Status = StatusCodes.Status422UnprocessableEntity;
                            break;

                        case ConcurrencyException concurrencyException:
                            context.Response.StatusCode = StatusCodes.Status409Conflict;
                            problemDetails.Title = "Concurrency conflict";
                            problemDetails.Detail = concurrencyException.Message;
                            problemDetails.Status = StatusCodes.Status409Conflict;
                            break;

                        default:
                            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                            problemDetails.Title = "An error occurred while processing your request.";
                            problemDetails.Status = StatusCodes.Status500InternalServerError;
                            break;
                    }

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
