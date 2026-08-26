using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using FluentValidation.AspNetCore;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Helpers;
using MyNovelBuilder.WebApi.Data.Repositories;
using MyNovelBuilder.WebApi.Middleware;
using MyNovelBuilder.WebApi.Extensions;
using MyNovelBuilder.WebApi.Models.Errors;
using MyNovelBuilder.WebApi.Options;
using MyNovelBuilder.WebApi.Services;
using MyNovelBuilder.WebApi.Services.ImageGeneration;
using MyNovelBuilder.WebApi.Services.TextGeneration;
using MyNovelBuilder.WebApi.Services.Tts;
using MyNovelBuilder.WebApi.Services.VideoGeneration;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

var dataFolder = Path.GetFullPath(
    builder.Configuration[AppStorageOptions.DataFolderKey]
    ?? Path.Combine(AppContext.BaseDirectory, "AppData"));
var staticFilesRoot = Path.Combine(dataFolder, "static");
Directory.CreateDirectory(dataFolder);

builder.Services.AddOptions<AppStorageOptions>()
    .Configure(options => options.DataFolder = dataFolder);

builder.Services.AddHttpContextAccessor();

// Add the controllers that contain the HTTP endpoints, and also configure
// the json serializer to use camelCase strings instead of integers for enums.
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        // Serialize enums to strings instead of integers
        var enumConverter = new JsonStringEnumConverter(JsonNamingPolicy.CamelCase);
        opts.JsonSerializerOptions.Converters.Add(enumConverter);
        opts.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
    });
    
// Add utilities to easily navigate the APIs via swagger, generating
// the file from the XML documentation around classes, methods and such.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(config =>
{
    config.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory,
        $"{Assembly.GetExecutingAssembly().GetName().Name}.xml"));
});

// Add routing and use lowercase URLs like /api/test instead of
// /api/Test.
builder.Services.AddRouting(options => options.LowercaseUrls = true);

// Compress responses with brotli or gzip (if not supported).
// By default, they use the fastest compression mode.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

// Add logging through serilog
var configuredDefaultLogLevel = builder.Configuration
    .GetValue<Microsoft.Extensions.Logging.LogLevel?>("Logging:LogLevel:Default")
    ?? Microsoft.Extensions.Logging.LogLevel.Information;
var defaultLogLevel = configuredDefaultLogLevel switch
{
    Microsoft.Extensions.Logging.LogLevel.Trace => LogEventLevel.Verbose,
    Microsoft.Extensions.Logging.LogLevel.Debug => LogEventLevel.Debug,
    Microsoft.Extensions.Logging.LogLevel.Information => LogEventLevel.Information,
    Microsoft.Extensions.Logging.LogLevel.Warning => LogEventLevel.Warning,
    Microsoft.Extensions.Logging.LogLevel.Error => LogEventLevel.Error,
    Microsoft.Extensions.Logging.LogLevel.Critical => LogEventLevel.Fatal,
    Microsoft.Extensions.Logging.LogLevel.None => LogEventLevel.Fatal,
    _ => LogEventLevel.Information
};

builder.Host.UseSerilog((_, options) =>
{
    // Keep the default concise. Full prompts, generated text, and provider
    // response bodies are logged at Debug and are only emitted when this
    // configured level is Debug (or Trace).
    options.MinimumLevel.Is(defaultLogLevel);

    // This will destructure JsonDocument and JsonElement when passed
    // as structured logs argument
    options.Destructure.With<JsonDestructuringPolicy>();

    // Do not log full request data
    options.MinimumLevel.Override("Microsoft.AspNetCore",
        LogEventLevel.Warning);

    // Log to the console sink, more sinks can be added here if needed
    options.WriteTo.Console();
});

// Configure the API error handler to return a JSON response with the
// error code and message upon validation errors, to make it
// consistent.
builder.Services.Configure<ApiBehaviorOptions>(x =>
{
    x.SuppressModelStateInvalidFilter = false;
    x.InvalidModelStateResponseFactory = ctx => new ApiErrorResult();
});

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite($"Data Source={dataFolder}/app.db");
});

builder.Services.AddScoped<INovelRepository, NovelRepository>();
builder.Services.AddScoped<ICompendiumRepository, CompendiumRepository>();
builder.Services.AddScoped<ICompendiumRecordRepository, CompendiumRecordRepository>();
builder.Services.AddScoped<IPromptRepository, PromptRepository>();
builder.Services.AddScoped<IVoiceRepository, VoiceRepository>();
builder.Services.AddScoped<IMediaFolderRepository, MediaFolderRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<INovelService, NovelService>();
builder.Services.AddScoped<INovelImportService, NovelImportService>();
builder.Services.AddScoped<ICompendiumService, CompendiumService>();
builder.Services.AddScoped<ICompendiumRecordService, CompendiumRecordService>();
builder.Services.AddScoped<IPromptService, PromptService>();
builder.Services.AddScoped<IVoiceService, VoiceService>();
builder.Services.AddScoped<IMediaFolderService, MediaFolderService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IWorldBuildingSessionService, WorldBuildingSessionService>();
builder.Services.AddScoped<INovelExportService, NovelExportService>();
builder.Services.AddScoped<ITextGenerationServiceResolver, TextGenerationServiceResolver>();
builder.Services.AddScoped<ITtsAudioGenerationService, TtsAudioGenerationService>();
builder.Services.AddScoped<IImmersiveTtsService, ImmersiveTtsService>();
builder.Services.AddSingleton<IAudioRepository, FileSystemWaveAudioRepository>();
builder.Services.AddSingleton<ITokenizerService, TokenizerService>();

builder.Services.AddSingleton<IIntegrationsService, IntegrationsService>();
builder.Services.AddSingleton<INovelPromptCreatorService, NovelPromptCreatorService>();
builder.Services.AddSingleton<ICompendiumPromptCreatorService, CompendiumPromptCreatorService>();
builder.Services.AddSingleton<IGenericPromptCreatorService, GenericPromptCreatorService>();
builder.Services.AddSingleton<IWorldBuildingPromptCreatorService, WorldBuildingPromptCreatorService>();

// Text generation services
builder.Services.RegisterKeyedServicesFromAssembly<ITextGenerationService>();

// TTS services
builder.Services.RegisterKeyedServicesFromAssembly<ITtsService>();

// Image generation services
builder.Services.RegisterKeyedServicesFromAssembly<IImageGenerationService>();

// Video generation services
builder.Services.RegisterKeyedServicesFromAssembly<IVideoGenerationService>();

// Mapster configuration
var config = new TypeAdapterConfig();
TypeAdapterConfig.GlobalSettings.Scan(Assembly.GetExecutingAssembly());
builder.Services.AddSingleton(config);
builder.Services.AddScoped<IMapper, ServiceMapper>();

// FluentValidation configuration
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>(includeInternalTypes: true);

builder.Services.AddHybridCache();

var app = builder.Build();

app.UseResponseCompression();

app.UseCors(b => b
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader()
    .WithExposedHeaders("Content-Disposition"));

// Enable swagger
app.UseSwagger();
app.UseSwaggerUI();

// Automatically apply migrations
using var scope = app.Services.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
await dbContext.Database.MigrateAsync();

app.UseMiddleware<ExceptionMiddleware>();

app.UseRouting();

Directory.CreateDirectory(staticFilesRoot);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(staticFilesRoot),
    RequestPath = "/static",
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append(
            "Access-Control-Allow-Origin", "*");
    }
});

app.Services.EnsurePostPutInputValidatorsAreRegistered();

app.MapControllers();

await app.RunAsync();

// This makes Program.cs visible to the test project, so we can use it
// with the WebApplicationFactory.
/// <summary></summary>
#pragma warning disable S1118
public partial class Program
{
}
#pragma warning restore S1118
