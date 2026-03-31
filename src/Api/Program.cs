using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Endpoints;
using Defra.WasteObligations.Api.Utils;
using Defra.WasteObligations.Api.Utils.Health;
using Defra.WasteObligations.Api.Utils.Logging;
using Defra.WasteObligations.Api.Utils.Metrics;
using Defra.WasteObligations.Api.Utils.Security;
using Elastic.CommonSchema.Serilog;
using Microsoft.AspNetCore.Diagnostics;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console(new EcsTextFormatter()).CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    var integrationTest = args.Contains("--integrationTest=true");

    builder.Configuration.AddEnvironmentVariables();

    builder.Services.AddCustomTrustStore(); // This must happen before Mongo and Http client connections
    builder.ConfigureLoggingAndTracing(integrationTest);
    builder.Services.Configure<RouteHandlerOptions>(o =>
    {
        // Without this, bad request detail will only be thrown in DEVELOPMENT mode
        o.ThrowOnBadRequest = true;
    });
    builder.Services.AddProblemDetails();
    builder.Services.AddHealth();
    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer<OpenApiDocumentTransformer>();
    });
    builder.Services.AddAuthenticationAuthorization();
    builder.Services.AddRequestMetrics();
    builder.Services.AddDbContext(builder.Configuration, integrationTest);
    builder.Services.AddValidation();

    var app = builder.Build();

    app.UseExceptionHandler(
        new ExceptionHandlerOptions
        {
            AllowStatusCode404Response = true,
            ExceptionHandler = async context =>
            {
                var exceptionHandlerFeature = context.Features.Get<IExceptionHandlerFeature>();
                var error = exceptionHandlerFeature?.Error;
                string? detail = null;

                if (error is BadHttpRequestException badHttpRequestException)
                {
                    context.Response.StatusCode = badHttpRequestException.StatusCode;
                    detail = badHttpRequestException.Message;
                }

                await context
                    .RequestServices.GetRequiredService<IProblemDetailsService>()
                    .WriteAsync(
                        new ProblemDetailsContext
                        {
                            HttpContext = context,
                            AdditionalMetadata = exceptionHandlerFeature?.Endpoint?.Metadata,
                            ProblemDetails = { Status = context.Response.StatusCode, Detail = detail },
                        }
                    );
            },
        }
    );
    app.UseHstsUnconditionally();
    app.UseHeaderPropagation();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapHealth();
    app.UseRequestMetrics();
    app.MapOpenApi("/documentation/openapi/{documentName}.json");
    app.UseReDoc(options =>
    {
        options.RoutePrefix = "documentation";
        options.SpecUrl = "/documentation/openapi/v1.json";
    });

    app.MapApiEndpoints();

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application start-up failed");
}
finally
{
    await Log.CloseAndFlushAsync();
}
