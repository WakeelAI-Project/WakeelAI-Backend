using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Wakeel.Application.Interfaces;

/// <summary>
/// Service for handling file uploads.
/// </summary>
public interface IFileService
{
    /// <summary>
    /// Saves a file stream and returns the relative path or URL to access it.
    /// </summary>
    Task<string> SaveFileAsync(Stream fileStream, string fileName, string folderName, CancellationToken cancellationToken = default);
}
