using System.Diagnostics;
using System.Text.Json.Serialization;
using TalentShortlist.Application.Contracts;
using TalentShortlist.Application.Services;
using TalentShortlist.Infrastructure.Demo;
using TalentShortlist.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
var openAiSettings = new OpenAiSettings
{
    ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? builder.Configuration["OpenAI:ApiKey"] ?? string.Empty,
    Model = builder.Configuration["OpenAI:Model"] ?? "gpt-5-mini",
    Endpoint = builder.Configuration["OpenAI:Endpoint"] ?? "https://api.openai.com/v1/chat/completions",
    TimeoutSeconds = int.TryParse(builder.Configuration["OpenAI:TimeoutSeconds"], out var timeoutSeconds) ? timeoutSeconds : 60
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
builder.Services.AddScoped<ShortlistService>();

var app = builder.Build();

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
app.MapControllers();
app.Run();

public partial class Program;
