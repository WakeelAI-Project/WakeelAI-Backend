namespace Wakeel.Application.Interfaces;

/// <summary>
/// Converts app-relative resource paths (e.g. "/uploads/documents/x.pdf")
/// into fully qualified absolute URLs usable outside the SPA (emails, external clients).
/// </summary>
public interface IPublicUrlBuilder
{
    string ToAbsoluteUrl(string? relativeOrAbsoluteUrl);
}
