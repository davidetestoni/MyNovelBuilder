using System.Security.Cryptography;
using System.Text.Json;
using MyNovelBuilder.WebApi.Enums;
using MyNovelBuilder.WebApi.Helpers;

namespace MyNovelBuilder.WebApi.Seeding;

internal static class SampleNovelSeedFixtureLoader
{
    internal const int SupportedSchemaVersion = 1;
    internal const string BundledFixtureRelativePath = "Seed/sample-novel";

    public static Task<SampleNovelSeedFixture> LoadBundledAsync(
        CancellationToken cancellationToken = default)
    {
        return LoadAsync(
            Path.Combine(AppContext.BaseDirectory, BundledFixtureRelativePath),
            cancellationToken);
    }

    internal static async Task<SampleNovelSeedFixture> LoadAsync(
        string fixtureRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fixtureRoot);

        var manifestPath = Path.Combine(fixtureRoot, "manifest.json");
        var manifest = await DeserializeAsync<SampleNovelSeedManifest>(
            manifestPath,
            cancellationToken);
        ValidateManifest(manifest, manifestPath);

        var prosePath = ResolveFixturePath(fixtureRoot, manifest.ProseFile, manifestPath);
        var prose = await DeserializeAsync<SampleProseSeedDefinition>(prosePath, cancellationToken);

        var compendia = new List<SampleCompendiumSeedDefinition>();
        foreach (var relativePath in manifest.CompendiumFiles)
        {
            var compendiumPath = ResolveFixturePath(fixtureRoot, relativePath, manifestPath);
            var file = await DeserializeAsync<SampleCompendiumSeedFile>(
                compendiumPath,
                cancellationToken);

            if (file.Compendia is null)
            {
                throw InvalidFixture(compendiumPath, "compendia must be an array");
            }

            compendia.AddRange(file.Compendia);
        }

        var fixture = new SampleNovelSeedFixture
        {
            RootPath = Path.GetFullPath(fixtureRoot),
            Manifest = manifest,
            Compendia = compendia,
            Prose = prose
        };
        ValidateContent(fixture, fixtureRoot);
        return fixture;
    }

    private static async Task<T> DeserializeAsync<T>(
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            var value = await JsonSerializer.DeserializeAsync<T>(
                stream,
                JsonDefaults.Options,
                cancellationToken);

            return value ?? throw InvalidFixture(path, "the document is empty");
        }
        catch (JsonException exception)
        {
            throw InvalidFixture(path, "the JSON document is malformed", exception);
        }
    }

    private static void ValidateManifest(
        SampleNovelSeedManifest manifest,
        string manifestPath)
    {
        if (manifest.SchemaVersion != SupportedSchemaVersion)
        {
            throw InvalidFixture(
                manifestPath,
                $"schemaVersion must be {SupportedSchemaVersion}");
        }

        ValidateText(manifest.Title, 100, "title", manifestPath);
        ValidateText(manifest.Author, 100, "author", manifestPath, allowEmpty: true);
        ValidateText(manifest.Brief, 500, "brief", manifestPath, allowEmpty: true);

        if (!Enum.IsDefined(manifest.Tense) ||
            !Enum.IsDefined(manifest.Pov) ||
            !Enum.IsDefined(manifest.Language))
        {
            throw InvalidFixture(manifestPath, "the novel contains an unsupported enum value");
        }

        ValidateText(manifest.MainCharacterKey, 100, "mainCharacterKey", manifestPath);
        ValidateText(manifest.ProseFile, 500, "proseFile", manifestPath);

        if (manifest.CompendiumFiles is null || manifest.CompendiumFiles.Count == 0)
        {
            throw InvalidFixture(manifestPath, "compendiumFiles must contain at least one path");
        }

        if (manifest.CompendiumFiles.Any(string.IsNullOrWhiteSpace))
        {
            throw InvalidFixture(manifestPath, "compendiumFiles must not contain blank paths");
        }

        if (manifest.Assets is null || manifest.Assets.Count == 0)
        {
            throw InvalidFixture(manifestPath, "assets must contain at least one item");
        }
    }

    private static void ValidateContent(
        SampleNovelSeedFixture fixture,
        string fixtureRoot)
    {
        if (fixture.Compendia.Count == 0)
        {
            throw InvalidFixture(fixtureRoot, "at least one compendium is required");
        }

        var compendiumKeys = new HashSet<string>(StringComparer.Ordinal);
        var recordKeys = new HashSet<string>(StringComparer.Ordinal);
        var recordTypes = new Dictionary<string, CompendiumRecordType>(StringComparer.Ordinal);

        for (var compendiumIndex = 0; compendiumIndex < fixture.Compendia.Count; compendiumIndex++)
        {
            var compendium = fixture.Compendia[compendiumIndex];
            var location = $"compendia[{compendiumIndex}]";
            if (compendium is null)
            {
                throw InvalidFixture(fixtureRoot, $"{location} must be an object");
            }

            ValidateUniqueKey(compendium.SeedKey, compendiumKeys, location, fixtureRoot);
            ValidateText(compendium.Name, 100, $"{location}.name", fixtureRoot);
            ValidateText(
                compendium.Description,
                500,
                $"{location}.description",
                fixtureRoot,
                allowEmpty: true);

            if (compendium.Records is null || compendium.Records.Count == 0)
            {
                throw InvalidFixture(fixtureRoot, $"{location}.records must not be empty");
            }

            for (var recordIndex = 0; recordIndex < compendium.Records.Count; recordIndex++)
            {
                var record = compendium.Records[recordIndex];
                var recordLocation = $"{location}.records[{recordIndex}]";
                if (record is null)
                {
                    throw InvalidFixture(fixtureRoot, $"{recordLocation} must be an object");
                }

                ValidateUniqueKey(record.SeedKey, recordKeys, recordLocation, fixtureRoot);
                ValidateText(record.Name, 100, $"{recordLocation}.name", fixtureRoot);
                ValidateText(
                    record.Aliases,
                    500,
                    $"{recordLocation}.aliases",
                    fixtureRoot,
                    allowEmpty: true);
                ValidateText(record.Context, 10000, $"{recordLocation}.context", fixtureRoot);

                if (!Enum.IsDefined(record.Type))
                {
                    throw InvalidFixture(fixtureRoot, $"{recordLocation}.type is not supported");
                }

                recordTypes.Add(record.SeedKey, record.Type);
            }
        }

        if (!recordTypes.TryGetValue(fixture.Manifest.MainCharacterKey, out var mainCharacterType))
        {
            throw InvalidFixture(fixtureRoot, "mainCharacterKey does not reference a record");
        }

        if (mainCharacterType != CompendiumRecordType.Character)
        {
            throw InvalidFixture(fixtureRoot, "mainCharacterKey must reference a character");
        }

        var proseImageKeys = ValidateAssets(fixture, recordKeys, fixtureRoot);
        ValidateProse(fixture.Prose, recordKeys, proseImageKeys, fixtureRoot);
    }

    private static IReadOnlySet<string> ValidateAssets(
        SampleNovelSeedFixture fixture,
        IReadOnlySet<string> recordKeys,
        string fixtureRoot)
    {
        var assetKeys = new HashSet<string>(StringComparer.Ordinal);
        var assetFiles = new HashSet<string>(StringComparer.Ordinal);
        var recordImageKeys = new HashSet<string>(StringComparer.Ordinal);
        var proseImageKeys = new HashSet<string>(StringComparer.Ordinal);
        var coverCount = 0;

        for (var index = 0; index < fixture.Manifest.Assets.Count; index++)
        {
            var asset = fixture.Manifest.Assets[index];
            var location = $"assets[{index}]";
            if (asset is null)
            {
                throw InvalidFixture(fixtureRoot, $"{location} must be an object");
            }

            ValidateUniqueKey(asset.SeedKey, assetKeys, location, fixtureRoot);
            ValidateText(asset.File, 500, $"{location}.file", fixtureRoot);
            ValidateText(asset.Sha256, 64, $"{location}.sha256", fixtureRoot);
            if (asset.Sha256.Length != 64 || asset.Sha256.Any(character => !Uri.IsHexDigit(character)))
            {
                throw InvalidFixture(fixtureRoot, $"{location}.sha256 must be a 64-character hexadecimal digest");
            }

            if (!Enum.IsDefined(asset.Kind))
            {
                throw InvalidFixture(fixtureRoot, $"{location}.kind is not supported");
            }

            var assetPath = ResolveFixturePath(fixtureRoot, asset.File, fixtureRoot);
            if (!assetFiles.Add(assetPath))
            {
                throw InvalidFixture(fixtureRoot, $"asset file '{asset.File}' is referenced more than once");
            }

            ValidateAssetDigest(assetPath, asset.Sha256, fixtureRoot);

            switch (asset.Kind)
            {
                case SampleNovelSeedAssetKind.Cover:
                    coverCount++;
                    ValidateNoRecordKey(asset, location, fixtureRoot);
                    break;
                case SampleNovelSeedAssetKind.RecordImage:
                    if (string.IsNullOrWhiteSpace(asset.RecordKey) || !recordKeys.Contains(asset.RecordKey))
                    {
                        throw InvalidFixture(
                            fixtureRoot,
                            $"{location}.recordKey must reference an existing record");
                    }

                    if (!recordImageKeys.Add(asset.RecordKey))
                    {
                        throw InvalidFixture(
                            fixtureRoot,
                            $"recordKey '{asset.RecordKey}' has more than one record image");
                    }

                    break;
                case SampleNovelSeedAssetKind.ProseImage:
                    ValidateNoRecordKey(asset, location, fixtureRoot);
                    proseImageKeys.Add(asset.SeedKey);
                    break;
                default:
                    throw InvalidFixture(fixtureRoot, $"{location}.kind is not supported");
            }
        }

        if (coverCount != 1)
        {
            throw InvalidFixture(fixtureRoot, "assets must contain exactly one cover");
        }

        return proseImageKeys;
    }

    private static void ValidateAssetDigest(
        string assetPath,
        string expectedDigest,
        string fixtureRoot)
    {
        if (!File.Exists(assetPath))
        {
            throw InvalidFixture(fixtureRoot, $"asset file '{assetPath}' does not exist");
        }

        try
        {
            using var stream = File.OpenRead(assetPath);
            var actualDigest = Convert.ToHexString(SHA256.HashData(stream));
            if (!actualDigest.Equals(expectedDigest, StringComparison.OrdinalIgnoreCase))
            {
                throw InvalidFixture(fixtureRoot, $"asset file '{assetPath}' does not match its sha256 digest");
            }
        }
        catch (IOException exception)
        {
            throw InvalidFixture(fixtureRoot, $"asset file '{assetPath}' could not be read", exception);
        }
    }

    private static void ValidateNoRecordKey(
        SampleNovelSeedAssetDefinition asset,
        string location,
        string fixtureRoot)
    {
        if (asset.RecordKey is not null)
        {
            throw InvalidFixture(fixtureRoot, $"{location}.recordKey is only valid for record images");
        }
    }

    private static void ValidateProse(
        SampleProseSeedDefinition prose,
        IReadOnlySet<string> recordKeys,
        IReadOnlySet<string> proseImageKeys,
        string fixtureRoot)
    {
        if (prose.Chapters is null || prose.Chapters.Count == 0)
        {
            throw InvalidFixture(fixtureRoot, "prose.chapters must not be empty");
        }

        for (var chapterIndex = 0; chapterIndex < prose.Chapters.Count; chapterIndex++)
        {
            var chapter = prose.Chapters[chapterIndex];
            var location = $"prose.chapters[{chapterIndex}]";
            if (chapter is null)
            {
                throw InvalidFixture(fixtureRoot, $"{location} must be an object");
            }

            ValidateText(chapter.Title, 200, $"{location}.title", fixtureRoot);
            if (chapter.Sections is null || chapter.Sections.Count == 0)
            {
                throw InvalidFixture(fixtureRoot, $"{location}.sections must not be empty");
            }

            if (chapter.StoryEvents is null)
            {
                throw InvalidFixture(fixtureRoot, $"{location}.storyEvents must be an array");
            }

            foreach (var storyEvent in chapter.StoryEvents)
            {
                if (storyEvent is null)
                {
                    throw InvalidFixture(fixtureRoot, $"{location}.storyEvents must contain objects");
                }

                ValidateText(storyEvent.Title, 200, $"{location}.storyEvents.title", fixtureRoot);
                ValidateText(
                    storyEvent.Date,
                    200,
                    $"{location}.storyEvents.date",
                    fixtureRoot,
                    allowEmpty: true);
                ValidateText(
                    storyEvent.Description,
                    5000,
                    $"{location}.storyEvents.description",
                    fixtureRoot,
                    allowEmpty: true);
            }

            foreach (var section in chapter.Sections)
            {
                if (section is null)
                {
                    throw InvalidFixture(fixtureRoot, $"{location}.sections must contain objects");
                }

                ValidateText(section.Summary, 10000, $"{location}.sections.summary", fixtureRoot);
                ValidateText(section.Text, 100000, $"{location}.sections.text", fixtureRoot);

                if (section.Images is null || section.RecordOverrides is null)
                {
                    throw InvalidFixture(
                        fixtureRoot,
                        $"{location}.sections images and recordOverrides must be arrays");
                }

                if (section.Images.Any(imageKey => !proseImageKeys.Contains(imageKey)))
                {
                    throw InvalidFixture(
                        fixtureRoot,
                        $"{location} contains an image that does not reference a prose image asset");
                }

                foreach (var recordOverride in section.RecordOverrides)
                {
                    if (recordOverride is null || !recordKeys.Contains(recordOverride.RecordKey))
                    {
                        throw InvalidFixture(
                            fixtureRoot,
                            $"{location} contains an override for an unknown recordKey");
                    }

                    ValidateText(recordOverride.Keyword, 200, "override.keyword", fixtureRoot);
                    ValidateText(
                        recordOverride.Description,
                        20000,
                        "override.description",
                        fixtureRoot,
                        allowEmpty: true);
                }
            }
        }
    }

    private static string ResolveFixturePath(
        string fixtureRoot,
        string relativePath,
        string manifestPath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw InvalidFixture(manifestPath, $"'{relativePath}' must be a relative path");
        }

        var normalizedRoot = Path.GetFullPath(fixtureRoot)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var resolvedPath = Path.GetFullPath(Path.Combine(normalizedRoot, relativePath));
        if (!resolvedPath.StartsWith(normalizedRoot, StringComparison.Ordinal))
        {
            throw InvalidFixture(manifestPath, $"'{relativePath}' leaves the fixture directory");
        }

        return resolvedPath;
    }

    private static void ValidateUniqueKey(
        string key,
        ISet<string> keys,
        string location,
        string fixturePath)
    {
        ValidateText(key, 100, $"{location}.seedKey", fixturePath);
        if (!keys.Add(key))
        {
            throw InvalidFixture(fixturePath, $"seedKey '{key}' is duplicated");
        }
    }

    private static void ValidateText(
        string value,
        int maximumLength,
        string location,
        string fixturePath,
        bool allowEmpty = false)
    {
        if (value is null || value.Length > maximumLength || (!allowEmpty && string.IsNullOrWhiteSpace(value)))
        {
            var minimum = allowEmpty ? 0 : 1;
            throw InvalidFixture(
                fixturePath,
                $"{location} must contain between {minimum} and {maximumLength} characters");
        }
    }

    private static InvalidDataException InvalidFixture(
        string fixturePath,
        string reason,
        Exception? innerException = null)
    {
        return new InvalidDataException(
            $"Sample novel fixture '{fixturePath}' is invalid: {reason}.",
            innerException);
    }
}
