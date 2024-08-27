using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using MyNovelBuilder.WebApi;
using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Helpers;
using MyNovelBuilder.WebApi.Data.Repositories;
using MyNovelBuilder.WebApi.Middleware;
using MyNovelBuilder.WebApi.Models.Errors;
using MyNovelBuilder.WebApi.Services;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

Globals.DataFolder = Path.GetFullPath(builder.Configuration["DataFolder"]!);
Directory.CreateDirectory(Globals.DataFolder);

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
builder.Host.UseSerilog((ctx, options) =>
{
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
    x.InvalidModelStateResponseFactory = ctx => new ApiErrorResult();
});

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite($"Data Source={Globals.DataFolder}/app.db");
});

builder.Services.AddScoped<INovelRepository, NovelRepository>();
builder.Services.AddScoped<ICompendiumRepository, CompendiumRepository>();
builder.Services.AddScoped<ICompendiumRecordRepository, CompendiumRecordRepository>();
builder.Services.AddScoped<IPromptRepository, PromptRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<INovelService, NovelService>();
builder.Services.AddScoped<ICompendiumService, CompendiumService>();
builder.Services.AddScoped<ICompendiumRecordService, CompendiumRecordService>();
builder.Services.AddScoped<IPromptService, PromptService>();

builder.Services.AddSingleton<IPromptCreatorService, PromptCreatorService>();
builder.Services.AddSingleton<ITextGenerationService, OpenRouterTextGenerationService>();
builder.Services.AddHttpClient<OpenRouterTextGenerationService>();

// Mapster configuration
var config = new TypeAdapterConfig();
TypeAdapterConfig.GlobalSettings.Scan(Assembly.GetExecutingAssembly());
builder.Services.AddSingleton(config);
builder.Services.AddScoped<IMapper, ServiceMapper>();

// FluentValidation configuration
builder.Services.AddValidatorsFromAssemblyContaining<Program>(includeInternalTypes: true);

var app = builder.Build();

app.UseCors(b => b
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

// Enable swagger
app.UseSwagger();
app.UseSwaggerUI();

// Automatically apply migrations
using var scope = app.Services.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
await dbContext.Database.MigrateAsync();

app.UseMiddleware<ExceptionMiddleware>();

app.UseRouting();

Directory.CreateDirectory(Globals.StaticFilesRoot);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Globals.StaticFilesRoot),
    RequestPath = "/static"
});

app.MapControllers();

app.UseResponseCompression();

await app.RunAsync();

// This makes Program.cs visible to the test project, so we can use it
// with the WebApplicationFactory.
/// <summary></summary>
#pragma warning disable S1118
public partial class Program
{
}
#pragma warning restore S1118
