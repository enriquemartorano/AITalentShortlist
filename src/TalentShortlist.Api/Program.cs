using System.Diagnostics;
using System.Text.Json.Serialization;
using Microsoft.Extensions.FileProviders;
using TalentShortlist.Application.Contracts;
using TalentShortlist.Application.Services;
using TalentShortlist.Infrastructure.Demo;
using TalentShortlist.Infrastructure.Repositories;
using TalentShortlist.Api.JobDescriptions;
using TalentShortlist.Api.Security;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
builder.Configuration.AddUserSecrets<Program>(optional: true);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
var openAiSettings = new OpenAiSettings
{
    ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? builder.Configuration["OpenAI:ApiKey"] ?? string.Empty,
    Model = builder.Configuration["OpenAI:Model"] ?? "gpt-5-mini",
    Endpoint = builder.Configuration["OpenAI:Endpoint"] ?? "https://api.openai.com/v1/chat/completions",
    TimeoutSeconds = int.TryParse(builder.Configuration["OpenAI:TimeoutSeconds"], out var timeoutSeconds) ? timeoutSeconds : 60,
    MaxAttempts = int.TryParse(builder.Configuration["OpenAI:MaxAttempts"], out var maxAttempts) ? maxAttempts : 3,
    ReasoningEffort = builder.Configuration["OpenAI:ReasoningEffort"] ?? "low",
    MaxCompletionTokens = int.TryParse(builder.Configuration["OpenAI:MaxCompletionTokens"], out var maxCompletionTokens) ? maxCompletionTokens : 8000
};
builder.Services.AddSingleton(openAiSettings);
builder.Services.AddHttpClient("OpenAI", client => client.Timeout = TimeSpan.FromSeconds(openAiSettings.TimeoutSeconds));
builder.Services.AddCors(options => options.AddPolicy("LocalFrontend", policy =>
    policy.WithOrigins("http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services.AddSingleton<ICandidateRepository, InMemoryCandidateRepository>();
builder.Services.AddSingleton<ICandidateEvaluator, DeterministicCandidateEvaluator>();
builder.Services.AddSingleton<IAiCandidateEvaluator, OpenAiCandidateEvaluator>();
builder.Services.AddSingleton<ICvTextExtractor, DemoCvTextExtractor>();
builder.Services.AddSingleton<IDemoDataService, DemoDataService>();
builder.Services.AddSingleton<JobDescriptionCatalog>();
builder.Services.AddSingleton<EvaluationRateLimiter>();
builder.Services.AddScoped<ShortlistService>();

var app = builder.Build();

var frontendPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "frontend"));
var frontendProvider = new PhysicalFileProvider(frontendPath);
app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = frontendProvider });
app.UseStaticFiles(new StaticFileOptions { FileProvider = frontendProvider });

app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    var startedAt = Stopwatch.GetTimestamp();

    logger.LogInformation("HTTP {Method} {Path} started", context.Request.Method, context.Request.Path);

    try
    {
        await next();
    }
    finally
    {
        logger.LogInformation(
            "HTTP {Method} {Path} completed with {StatusCode} in {ElapsedMilliseconds:0} ms",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
    }
});

app.UseCors("LocalFrontend");
app.Use(async (context, next) =>
{
    if (HttpMethods.IsPost(context.Request.Method) && context.Request.Path.Equals("/api/shortlists/evaluate"))
    {
        var limiter = context.RequestServices.GetRequiredService<EvaluationRateLimiter>();
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (!limiter.TryAcquire(ipAddress, out var reason))
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = "3600";
            await context.Response.WriteAsJsonAsync(new { title = "Evaluation rate limit exceeded", detail = reason });
            return;
        }
    }

    await next();
});
app.MapControllers();
app.Run();

public partial class Program;
