using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Helpers;
using MyNovelBuilder.WebApi.Models.Novels;
using MyNovelBuilder.WebApi.Options;
using MyNovelBuilder.WebApi.Services;
using SixLabors.ImageSharp;

namespace MyNovelBuilder.WebApi.Seeding;

internal sealed class SampleNovelSeeder
{
    internal const string MarkerKey = "sample-novel";

    private readonly AppDbContext _dbContext;
    private readonly ITokenizerService _tokenizerService;
    private readonly AppStorageOptions _storageOptions;
    private readonly SeedOptions _seedOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SampleNovelSeeder> _logger;
    private readonly Func<CancellationToken, Task<SampleNovelSeedFixture>> _loadFixtureAsync;

    public SampleNovelSeeder(
        AppDbContext dbContext,
        ITokenizerService tokenizerService,
        IOptions<AppStorageOptions> storageOptions,
        IOptions<SeedOptions> seedOptions,
        TimeProvider timeProvider,
        ILogger<SampleNovelSeeder> logger)
        : this(
            dbContext,
            tokenizerService,
            storageOptions.Value,
            seedOptions.Value,
            timeProvider,
            logger,
            SampleNovelSeedFixtureLoader.LoadBundledAsync)
    {
    }

    internal SampleNovelSeeder(
        AppDbContext dbContext,
        ITokenizerService tokenizerService,
        AppStorageOptions storageOptions,
        SeedOptions seedOptions,
        TimeProvider timeProvider,
        ILogger<SampleNovelSeeder> logger,
        Func<CancellationToken, Task<SampleNovelSeedFixture>> loadFixtureAsync)
    {
        _dbContext = dbContext;
        _tokenizerService = tokenizerService;
        _storageOptions = storageOptions;
        _seedOptions = seedOptions;
        _timeProvider = timeProvider;
        _logger = logger;
        _loadFixtureAsync = loadFixtureAsync;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await IsCompleteAsync(cancellationToken))
        {
            return;
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            cancellationToken);

        if (await IsCompleteAsync(cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        if (!_seedOptions.IncludeSampleNovel)
        {
            _logger.LogInformation("The one-time sample novel import is disabled.");
            AddCompletionMarker();
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var fixture = await _loadFixtureAsync(cancellationToken);
        var preparedImport = PrepareImport(fixture);
        var stagingRoot = Path.Combine(
            _storageOptions.DataFolder,
            ".seed-staging",
            Guid.NewGuid().ToString("N"));
        var installedDirectories = new List<string>();

        try
        {
            await StageFilesAsync(
                fixture,
                preparedImport,
                stagingRoot,
                cancellationToken);
            EnsureDestinationsAreAvailable(preparedImport.InstallDirectories);

            _dbContext.Novels.Add(preparedImport.Novel);
            AddCompletionMarker();
            await _dbContext.SaveChangesAsync(cancellationToken);

            InstallStagedDirectories(
                stagingRoot,
                preparedImport.InstallDirectories,
                installedDirectories);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Imported sample novel {SampleNovelTitle} with ID {SampleNovelId} without modifying existing novels.",
                preparedImport.Novel.Title,
                preparedImport.Novel.Id);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            DeleteDirectories(installedDirectories);
            _dbContext.ChangeTracker.Clear();
            throw;
        }
        finally
        {
            DeleteDirectory(stagingRoot);
            DeleteDirectoryIfEmpty(Path.GetDirectoryName(stagingRoot)!);
        }
    }

    private PreparedSampleImport PrepareImport(SampleNovelSeedFixture fixture)
    {
        var assetIds = fixture.Manifest.Assets.ToDictionary(
            asset => asset.SeedKey,
            _ => Guid.NewGuid(),
            StringComparer.Ordinal);
        var recordsByKey = new Dictionary<string, CompendiumRecord>(StringComparer.Ordinal);
        var compendiaByKey = new Dictionary<string, Compendium>(StringComparer.Ordinal);

        foreach (var compendiumDefinition in fixture.Compendia)
        {
            var records = compendiumDefinition.Records.Select(recordDefinition =>
            {
                var record = new CompendiumRecord
                {
                    Id = Guid.NewGuid(),
                    Name = recordDefinition.Name,
                    Aliases = recordDefinition.Aliases,
                    Type = recordDefinition.Type,
                    Context = recordDefinition.Context,
                    ContextTokenCount = _tokenizerService.CountTokens(recordDefinition.Context),
                    AlwaysIncluded = recordDefinition.AlwaysIncluded
                };
                recordsByKey.Add(recordDefinition.SeedKey, record);
                return record;
            }).ToList();

            var compendium = new Compendium
            {
                Id = Guid.NewGuid(),
                Name = compendiumDefinition.Name,
                Description = compendiumDefinition.Description,
                Records = records
            };
            foreach (var record in records)
            {
                record.Compendium = compendium;
            }

            compendiaByKey.Add(compendiumDefinition.SeedKey, compendium);
        }

        foreach (var asset in fixture.Manifest.Assets.Where(
                     asset => asset.Kind == SampleNovelSeedAssetKind.RecordImage))
        {
            recordsByKey[asset.RecordKey!].CurrentImageId = assetIds[asset.SeedKey];
        }

        var novel = new Novel
        {
            Id = Guid.NewGuid(),
            Title = fixture.Manifest.Title,
            Author = fixture.Manifest.Author,
            Brief = fixture.Manifest.Brief,
            Tense = fixture.Manifest.Tense,
            Pov = fixture.Manifest.Pov,
            Language = fixture.Manifest.Language,
            RpgMode = fixture.Manifest.RpgMode,
            Compendia = compendiaByKey.Values.ToList(),
            MainCharacter = recordsByKey[fixture.Manifest.MainCharacterKey]
        };
        var prose = CreateProse(fixture.Prose, recordsByKey, assetIds);

        var installDirectories = new List<string>
        {
            Path.Combine("novels", novel.Id.ToString()),
            Path.Combine("static", "novels", novel.Id.ToString())
        };
        installDirectories.AddRange(
            fixture.Manifest.Assets
                .Where(asset => asset.Kind == SampleNovelSeedAssetKind.RecordImage)
                .Select(asset => recordsByKey[asset.RecordKey!].Compendium.Id)
                .Distinct()
                .Select(compendiumId => Path.Combine(
                    "static",
                    "compendium",
                    compendiumId.ToString())));

        return new PreparedSampleImport(
            novel,
            prose,
            recordsByKey,
            assetIds,
            installDirectories);
    }

    private static Prose CreateProse(
        SampleProseSeedDefinition definition,
        IReadOnlyDictionary<string, CompendiumRecord> recordsByKey,
        IReadOnlyDictionary<string, Guid> assetIds)
    {
        return new Prose
        {
            Chapters = definition.Chapters.Select(chapter => new Chapter
            {
                Title = chapter.Title,
                StoryEvents = chapter.StoryEvents.Select(storyEvent => new StoryEvent
                {
                    Title = storyEvent.Title,
                    Date = storyEvent.Date,
                    Description = storyEvent.Description
                }).ToArray(),
                Sections = chapter.Sections.Select(section => new Section
                {
                    Summary = section.Summary,
                    Text = section.Text,
                    Images = section.Images.Select(
                        imageKey => $"{assetIds[imageKey]}.png").ToArray(),
                    RecordOverrides = section.RecordOverrides.Select(recordOverride =>
                        new RecordOverride
                        {
                            CompendiumRecordId = recordsByKey[recordOverride.RecordKey].Id,
                            Keyword = recordOverride.Keyword,
                            Description = recordOverride.Description
                        }).ToArray()
                }).ToList()
            }).ToList()
        };
    }

    private static async Task StageFilesAsync(
        SampleNovelSeedFixture fixture,
        PreparedSampleImport preparedImport,
        string stagingRoot,
        CancellationToken cancellationToken)
    {
        var prosePath = Path.Combine(
            stagingRoot,
            "novels",
            preparedImport.Novel.Id.ToString(),
            "prose.json");
        Directory.CreateDirectory(Path.GetDirectoryName(prosePath)!);
        await File.WriteAllTextAsync(
            prosePath,
            JsonSerializer.Serialize(preparedImport.Prose, JsonDefaults.Options),
            cancellationToken);

        foreach (var asset in fixture.Manifest.Assets)
        {
            var sourcePath = Path.Combine(fixture.RootPath, asset.File);
            var assetId = preparedImport.AssetIds[asset.SeedKey];
            string destinationPath;
            switch (asset.Kind)
            {
                case SampleNovelSeedAssetKind.Cover:
                    destinationPath = Path.Combine(
                        stagingRoot,
                        "static",
                        "novels",
                        preparedImport.Novel.Id.ToString(),
                        $"cover_{assetId}.png");
                    break;
                case SampleNovelSeedAssetKind.RecordImage:
                    var record = preparedImport.RecordsByKey[asset.RecordKey!];
                    destinationPath = Path.Combine(
                        stagingRoot,
                        "static",
                        "compendium",
                        record.Compendium.Id.ToString(),
                        "records",
                        record.Id.ToString(),
                        "gallery",
                        $"{assetId}.png");
                    break;
                case SampleNovelSeedAssetKind.ProseImage:
                    destinationPath = Path.Combine(
                        stagingRoot,
                        "static",
                        "novels",
                        preparedImport.Novel.Id.ToString(),
                        "prose-images",
                        $"{assetId}.png");
                    break;
                default:
                    throw new InvalidDataException($"Unsupported sample asset kind '{asset.Kind}'.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            using var image = await Image.LoadAsync(sourcePath, cancellationToken);
            await image.SaveAsPngAsync(destinationPath, cancellationToken);
        }
    }

    private void EnsureDestinationsAreAvailable(IEnumerable<string> relativeDirectories)
    {
        foreach (var relativeDirectory in relativeDirectories)
        {
            var destination = Path.Combine(_storageOptions.DataFolder, relativeDirectory);
            if (Directory.Exists(destination) || File.Exists(destination))
            {
                throw new IOException(
                    $"Refusing to overwrite existing sample import destination '{destination}'.");
            }
        }
    }

    private void InstallStagedDirectories(
        string stagingRoot,
        IEnumerable<string> relativeDirectories,
        ICollection<string> installedDirectories)
    {
        foreach (var relativeDirectory in relativeDirectories)
        {
            var source = Path.Combine(stagingRoot, relativeDirectory);
            var destination = Path.Combine(_storageOptions.DataFolder, relativeDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            Directory.Move(source, destination);
            installedDirectories.Add(destination);
        }
    }

    private void DeleteDirectories(IEnumerable<string> paths)
    {
        foreach (var path in paths.Reverse())
        {
            DeleteDirectory(path);
        }
    }

    private void DeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(
                exception,
                "Could not clean up sample novel import directory {Directory}.",
                path);
        }
    }

    private static void DeleteDirectoryIfEmpty(string path)
    {
        if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
        {
            Directory.Delete(path);
        }
    }

    private void AddCompletionMarker()
    {
        _dbContext.InitializationMarkers.Add(new InitializationMarker
        {
            Key = MarkerKey,
            CompletedAtUtc = _timeProvider.GetUtcNow().UtcDateTime
        });
    }

    private Task<bool> IsCompleteAsync(CancellationToken cancellationToken)
    {
        return _dbContext.InitializationMarkers.AnyAsync(
            marker => marker.Key == MarkerKey,
            cancellationToken);
    }

    private sealed record PreparedSampleImport(
        Novel Novel,
        Prose Prose,
        IReadOnlyDictionary<string, CompendiumRecord> RecordsByKey,
        IReadOnlyDictionary<string, Guid> AssetIds,
        IReadOnlyList<string> InstallDirectories);
}
