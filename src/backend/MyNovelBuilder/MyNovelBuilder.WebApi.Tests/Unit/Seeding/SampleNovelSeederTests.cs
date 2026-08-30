using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MyNovelBuilder.WebApi.Data;
using MyNovelBuilder.WebApi.Data.Entities;
using MyNovelBuilder.WebApi.Helpers;
using MyNovelBuilder.WebApi.Models.Novels;
using MyNovelBuilder.WebApi.Options;
using MyNovelBuilder.WebApi.Seeding;
using MyNovelBuilder.WebApi.Services;

namespace MyNovelBuilder.WebApi.Tests.Unit.Seeding;

public sealed class SampleNovelSeederTests : IAsyncDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly AppDbContext dbContext;
    private readonly string dataFolder = Path.Combine(
        Path.GetTempPath(),
        $"mynovelbuilder-sample-seeder-tests-{Guid.NewGuid():N}");

    public SampleNovelSeederTests()
    {
        Directory.CreateDirectory(dataFolder);
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        dbContext = new AppDbContext(options);
        dbContext.Database.EnsureCreated();
    }

    [Fact]
    public async Task SeedAsync_AddsBundledSampleToExistingUnmarkedDatabaseWithoutChangingData()
    {
        var existingNovel = new Novel { Title = "Existing novel" };
        dbContext.Novels.Add(existingNovel);
        await dbContext.SaveChangesAsync();
        var existingFile = Path.Combine(
            dataFolder,
            "novels",
            existingNovel.Id.ToString(),
            "keep.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(existingFile)!);
        await File.WriteAllTextAsync(existingFile, "keep me");

        await CreateSeeder().SeedAsync();

        Assert.Equal(2, await dbContext.Novels.CountAsync());
        Assert.Equal("keep me", await File.ReadAllTextAsync(existingFile));
        var sample = await dbContext.Novels
            .Include(novel => novel.MainCharacter)
            .Include(novel => novel.Compendia)
            .ThenInclude(compendium => compendium.Records)
            .SingleAsync(novel => novel.Id != existingNovel.Id);
        Assert.Contains("Sample Novel", sample.Title);
        Assert.False(sample.RpgMode);
        Assert.Equal(3, sample.Compendia.Count());
        Assert.Equal(10, sample.Compendia.SelectMany(compendium => compendium.Records).Count());
        Assert.NotNull(sample.MainCharacter);
        Assert.Equal("Mira Vey", sample.MainCharacter.Name);

        var prosePath = Path.Combine(dataFolder, "novels", sample.Id.ToString(), "prose.json");
        var prose = JsonSerializer.Deserialize<Prose>(
            await File.ReadAllTextAsync(prosePath),
            JsonDefaults.Options)!;
        Assert.Equal(3, prose.Chapters.Count);
        var proseImage = Assert.Single(
            prose.Chapters.SelectMany(chapter => chapter.Sections).SelectMany(section => section.Images));
        Assert.EndsWith(".png", proseImage);

        var sampleStaticDirectory = Path.Combine(dataFolder, "static", "novels", sample.Id.ToString());
        Assert.Equal(
            2,
            Directory.GetFiles(sampleStaticDirectory, "*.png", SearchOption.AllDirectories).Length);
        Assert.Equal(
            6,
            Directory.GetFiles(
                Path.Combine(dataFolder, "static", "compendium"),
                "*.png",
                SearchOption.AllDirectories).Length);
        Assert.True(await dbContext.InitializationMarkers.AnyAsync(
            marker => marker.Key == SampleNovelSeeder.MarkerKey));
    }

    [Fact]
    public async Task SeedAsync_DoesNotReloadOrOverwriteEditedSampleOnRepeatStartup()
    {
        var fixtureLoadCount = 0;
        var seeder = CreateSeeder(async cancellationToken =>
        {
            fixtureLoadCount++;
            return await SampleNovelSeedFixtureLoader.LoadBundledAsync(cancellationToken);
        });
        await seeder.SeedAsync();
        var sample = await dbContext.Novels.SingleAsync();
        dbContext.Entry(sample).Property(novel => novel.Title).CurrentValue =
            "My edited example";
        await dbContext.SaveChangesAsync();

        await seeder.SeedAsync();

        Assert.Equal(1, fixtureLoadCount);
        Assert.Equal("My edited example", (await dbContext.Novels.SingleAsync()).Title);
        Assert.Equal(
            1,
            await dbContext.InitializationMarkers.CountAsync(
                marker => marker.Key == SampleNovelSeeder.MarkerKey));
    }

    [Fact]
    public async Task SeedAsync_DoesNotRestoreDeletedSample()
    {
        var seeder = CreateSeeder();
        await seeder.SeedAsync();
        var sample = await dbContext.Novels.SingleAsync();
        dbContext.Novels.Remove(sample);
        await dbContext.SaveChangesAsync();

        await seeder.SeedAsync();

        Assert.Empty(await dbContext.Novels.ToListAsync());
    }

    [Fact]
    public async Task SeedAsync_DisabledOptionWritesMarkerWithoutImporting()
    {
        var seeder = CreateSeeder(includeSampleNovel: false);

        await seeder.SeedAsync();

        Assert.Empty(await dbContext.Novels.ToListAsync());
        Assert.True(await dbContext.InitializationMarkers.AnyAsync(
            marker => marker.Key == SampleNovelSeeder.MarkerKey));
        Assert.False(Directory.Exists(Path.Combine(dataFolder, "novels")));
    }

    [Fact]
    public async Task SeedAsync_FixtureFailureLeavesDatabaseAndFilesUntouched()
    {
        var seeder = CreateSeeder(
            _ => throw new InvalidDataException("Broken fixture"));

        await Assert.ThrowsAsync<InvalidDataException>(() => seeder.SeedAsync());

        Assert.Empty(await dbContext.Novels.ToListAsync());
        Assert.Empty(await dbContext.InitializationMarkers.ToListAsync());
        Assert.Empty(Directory.EnumerateFileSystemEntries(dataFolder));
    }

    [Fact]
    public async Task SeedAsync_MissingAssetDuringStagingRollsBackDatabaseAndFiles()
    {
        var existingNovel = new Novel { Title = "Existing novel" };
        dbContext.Novels.Add(existingNovel);
        await dbContext.SaveChangesAsync();
        var fixture = await CopyAndLoadBundledFixtureAsync();
        File.Delete(Path.Combine(
            fixture.RootPath,
            fixture.Manifest.Assets[0].File));
        var seeder = CreateSeeder(_ => Task.FromResult(fixture));

        await Assert.ThrowsAnyAsync<IOException>(() => seeder.SeedAsync());

        var preservedNovel = await dbContext.Novels.SingleAsync();
        Assert.Equal(existingNovel.Id, preservedNovel.Id);
        Assert.Empty(await dbContext.InitializationMarkers.ToListAsync());
        Assert.False(Directory.Exists(Path.Combine(dataFolder, "novels")));
        Assert.False(Directory.Exists(Path.Combine(dataFolder, "static")));
        Assert.False(Directory.Exists(Path.Combine(dataFolder, ".seed-staging")));
    }

    public async ValueTask DisposeAsync()
    {
        await dbContext.DisposeAsync();
        await connection.DisposeAsync();
        if (Directory.Exists(dataFolder))
        {
            Directory.Delete(dataFolder, recursive: true);
        }
    }

    private SampleNovelSeeder CreateSeeder(
        Func<CancellationToken, Task<SampleNovelSeedFixture>>? loadFixtureAsync = null,
        bool includeSampleNovel = true)
    {
        return new SampleNovelSeeder(
            dbContext,
            new FakeTokenizerService(),
            new AppStorageOptions { DataFolder = dataFolder },
            new SeedOptions { IncludeSampleNovel = includeSampleNovel },
            new FixedTimeProvider(
                new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero)),
            NullLogger<SampleNovelSeeder>.Instance,
            loadFixtureAsync ?? SampleNovelSeedFixtureLoader.LoadBundledAsync);
    }

    private async Task<SampleNovelSeedFixture> CopyAndLoadBundledFixtureAsync()
    {
        var sourceRoot = Path.Combine(
            AppContext.BaseDirectory,
            SampleNovelSeedFixtureLoader.BundledFixtureRelativePath);
        var fixtureRoot = Path.Combine(dataFolder, "fixture-source");

        foreach (var sourcePath in Directory.GetFiles(
                     sourceRoot,
                     "*",
                     SearchOption.AllDirectories))
        {
            var destinationPath = Path.Combine(
                fixtureRoot,
                Path.GetRelativePath(sourceRoot, sourcePath));
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(sourcePath, destinationPath);
        }

        return await SampleNovelSeedFixtureLoader.LoadAsync(fixtureRoot);
    }

    private sealed class FakeTokenizerService : ITokenizerService
    {
        public int CountTokens(string text) => text.Length;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
