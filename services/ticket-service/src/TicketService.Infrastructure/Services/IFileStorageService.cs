namespace TicketService.Infrastructure.Services;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(Stream fileStream, string fileName, string subDirectory);
    Stream OpenFileAsync(string fileUrl);
    Task DeleteFileAsync(string fileUrl);
}
