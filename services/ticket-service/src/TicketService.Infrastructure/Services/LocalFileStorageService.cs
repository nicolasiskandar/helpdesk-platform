namespace TicketService.Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _uploadsDirectory;

    public LocalFileStorageService(string uploadsDirectory)
    {
        _uploadsDirectory = uploadsDirectory;
        Directory.CreateDirectory(_uploadsDirectory);
    }

    public async Task<string> SaveFileAsync(Stream fileStream, string fileName, string subDirectory)
    {
        var targetDir = Path.Combine(_uploadsDirectory, subDirectory);
        Directory.CreateDirectory(targetDir);

        var safeFileName = Path.GetFileName(fileName);
        var uniqueName = $"{Guid.NewGuid()}_{safeFileName}";
        var filePath = Path.Combine(targetDir, uniqueName);

        using var fileStream2 = File.Create(filePath);
        await fileStream.CopyToAsync(fileStream2);

        return $"{subDirectory}/{uniqueName}";
    }

    public Stream OpenFileAsync(string fileUrl)
    {
        var fullPath = Path.Combine(_uploadsDirectory, fileUrl);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("File not found.", fileUrl);
        return File.OpenRead(fullPath);
    }

    public Task DeleteFileAsync(string fileUrl)
    {
        var fullPath = Path.Combine(_uploadsDirectory, fileUrl);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
        return Task.CompletedTask;
    }
}
