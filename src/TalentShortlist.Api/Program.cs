using System.Diagnostics;
using TalentShortlist.Application.Contracts;
using TalentShortlist.Application.Services;
using TalentShortlist.Infrastructure.Demo;
using TalentShortlist.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpClient();
builder.Services.AddCors(options => options.AddPolicy("LocalFrontend", policy =>
    policy.WithOrigins("http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services.AddSingleton<ICandidateRepository, InMemoryCandidateRepository>();
builder.Services.AddSingleton<ICandidateEvaluator, DeterministicCandidateEvaluator>();
builder.Services.AddSingleton<IAiCandidateEvaluator>(serviceProvider =>
    new OpenAiCandidateEvaluator(
        serviceProvider.GetRequiredService<ICandidateEvaluator>(),
        serviceProvider.GetRequiredService<ILogger<OpenAiCandidateEvaluator>>(),
        new OpenAiSettings
        {
            ApiKey = builder.Configuration["OpenAI:ApiKey"] ?? string.Empty,
            Model = builder.Configuration["OpenAI:Model"] ?? string.Empty,
            Endpoint = builder.Configuration["OpenAI:Endpoint"] ?? "https://api.openai.com/v1/chat/completions"
        }));
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
