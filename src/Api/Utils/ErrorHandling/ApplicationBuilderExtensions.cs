using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Defra.WasteObligations.Api.Data;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Metadata;
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
                    var exceptionHandlerFeature =
                        context.Features.Get<IExceptionHandlerFeature>()
                        ?? throw new InvalidOperationException("Exception handler feature is unavailable.");
                    var error = exceptionHandlerFeature.Error;
                    var (statusCode, title, detail) = error switch
                    {
                        BadHttpRequestException ex => (
                            ex.StatusCode,
                            "Bad request",
                            GetEnumValidationDetail(ex, exceptionHandlerFeature.Endpoint) ?? ex.Message
                        ),
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
                                AdditionalMetadata = exceptionHandlerFeature.Endpoint?.Metadata,
                                ProblemDetails = problemDetails,
                            }
                        );
                },
            }
        );
    }

    private static string? GetEnumValidationDetail(BadHttpRequestException exception, Endpoint? endpoint)
    {
        if (
            exception.InnerException is not JsonException { Path: { } path }
            || endpoint?.Metadata.GetMetadata<IAcceptsMetadata>()?.RequestType is not { } requestType
        )
            return null;

        var propertyName = path.TrimStart('$', '.');
        if (string.IsNullOrEmpty(propertyName) || propertyName.Contains('.'))
            return null;

        var property = requestType
            .GetProperties()
            .FirstOrDefault(x => x.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name == propertyName);
        var enumType = property is null
            ? null
            : Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (enumType is null || !enumType.IsEnum)
            return null;

        return $"The value for '{propertyName}' must be one of: {string.Join(", ", Enum.GetNames(enumType))}.";
    }
}
