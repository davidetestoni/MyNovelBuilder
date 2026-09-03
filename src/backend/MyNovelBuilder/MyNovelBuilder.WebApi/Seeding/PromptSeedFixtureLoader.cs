using System.Text.Json;
using MyNovelBuilder.WebApi.Helpers;

namespace MyNovelBuilder.WebApi.Seeding;

internal static class PromptSeedFixtureLoader
{
    internal const int SupportedSchemaVersion = 1;
    internal const string BundledFixtureRelativePath = "Seed/prompts.json";

    public static Task<PromptSeedFixture> LoadBundledAsync(
        CancellationToken cancellationToken = default)
    {
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            BundledFixtureRelativePath);

        return LoadAsync(fixturePath, cancellationToken);
    }

    internal static async Task<PromptSeedFixture> LoadAsync(
        string fixturePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fixturePath);

        try
        {
            await using var fixtureStream = File.OpenRead(fixturePath);
            var fixture = await JsonSerializer.DeserializeAsync<PromptSeedFixture>(
                fixtureStream,
                JsonDefaults.Options,
                cancellationToken);

            if (fixture is null)
            {
                throw InvalidFixture(fixturePath, "the document is empty");
            }

            Validate(fixture, fixturePath);
            return fixture;
        }
        catch (JsonException exception)
        {
            throw InvalidFixture(fixturePath, "the JSON document is malformed", exception);
        }
    }

    private static void Validate(PromptSeedFixture fixture, string fixturePath)
    {
        if (fixture.SchemaVersion != SupportedSchemaVersion)
        {
            throw InvalidFixture(
                fixturePath,
                $"schemaVersion must be {SupportedSchemaVersion}");
        }

        if (fixture.Prompts is null)
        {
            throw InvalidFixture(fixturePath, "prompts must be an array");
        }

        var seedKeys = new HashSet<string>(StringComparer.Ordinal);
        for (var promptIndex = 0; promptIndex < fixture.Prompts.Count; promptIndex++)
        {
            var prompt = fixture.Prompts[promptIndex];
            var promptLocation = $"prompts[{promptIndex}]";

            if (prompt is null)
            {
                throw InvalidFixture(fixturePath, $"{promptLocation} must be an object");
            }

            if (string.IsNullOrWhiteSpace(prompt.SeedKey))
            {
                throw InvalidFixture(
                    fixturePath,
                    $"{promptLocation}.seedKey must not be blank");
            }

            if (!seedKeys.Add(prompt.SeedKey))
            {
                throw InvalidFixture(
                    fixturePath,
                    $"seedKey '{prompt.SeedKey}' is duplicated");
            }

            if (prompt.SeedVersion < 1)
            {
                throw InvalidFixture(
                    fixturePath,
                    $"{promptLocation}.seedVersion must be at least 1");
            }

            if (string.IsNullOrWhiteSpace(prompt.Name) || prompt.Name.Length > 100)
            {
                throw InvalidFixture(
                    fixturePath,
                    $"{promptLocation}.name must contain between 1 and 100 characters");
            }

            if (!Enum.IsDefined(prompt.Type))
            {
                throw InvalidFixture(
                    fixturePath,
                    $"{promptLocation}.type is not supported");
            }

            ValidateMessages(prompt.Messages, promptLocation, fixturePath);
        }
    }

    private static void ValidateMessages(
        IReadOnlyList<PromptSeedMessage> messages,
        string promptLocation,
        string fixturePath)
    {
        if (messages is null || messages.Count == 0)
        {
            throw InvalidFixture(
                fixturePath,
                $"{promptLocation}.messages must contain at least one message");
        }

        for (var messageIndex = 0; messageIndex < messages.Count; messageIndex++)
        {
            var message = messages[messageIndex];
            var messageLocation = $"{promptLocation}.messages[{messageIndex}]";

            if (message is null)
            {
                throw InvalidFixture(fixturePath, $"{messageLocation} must be an object");
            }

            if (!Enum.IsDefined(message.Role))
            {
                throw InvalidFixture(
                    fixturePath,
                    $"{messageLocation}.role is not supported");
            }

            if (string.IsNullOrWhiteSpace(message.Message) || message.Message.Length > 50000)
            {
                throw InvalidFixture(
                    fixturePath,
                    $"{messageLocation}.message must contain between 1 and 50000 characters");
            }
        }
    }

    private static InvalidDataException InvalidFixture(
        string fixturePath,
        string reason,
        Exception? innerException = null)
    {
        return new InvalidDataException(
            $"Prompt seed fixture '{fixturePath}' is invalid: {reason}.",
            innerException);
    }
}
