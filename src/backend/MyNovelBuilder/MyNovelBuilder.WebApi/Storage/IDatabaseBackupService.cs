namespace MyNovelBuilder.WebApi.Storage;

internal interface IDatabaseBackupService
{
    string? CreateBackup();
}
