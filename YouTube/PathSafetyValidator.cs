using OnlineTeamTools.MCP.YouTube.Tools;

namespace OnlineTeamTools.MCP.YouTube.YouTube;

public sealed class PathSafetyValidator
{
    private readonly YouTubeOptions _options;
    private readonly StringComparison _pathComparison;

    public PathSafetyValidator(YouTubeOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    public string ValidateVideoPath(string inputPath)
    {
        var fullPath = ValidatePathUnderRoot(inputPath);
        ValidateFile(fullPath, _options.AllowedVideoExtensions, "video");
        return fullPath;
    }

    public string ValidateImagePath(string inputPath)
    {
        var fullPath = ValidatePathUnderRoot(inputPath);
        ValidateFile(fullPath, _options.AllowedImageExtensions, "image");
        return fullPath;
    }

    private string ValidatePathUnderRoot(string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new ToolExecutionException("Path is required.", Protocol.JsonRpcError.InvalidParamsCode);
        }

        var candidatePath = Path.IsPathRooted(inputPath)
            ? inputPath
            : Path.Combine(_options.AllowedRoot, inputPath);

        var fullPath = Path.GetFullPath(candidatePath);

        if (!IsPathUnderAllowedRoot(fullPath))
        {
            throw new ToolExecutionException(
                "Path is outside YOUTUBE_ALLOWED_ROOT.",
                Protocol.JsonRpcError.InvalidParamsCode,
                new { allowed_root = _options.AllowedRoot });
        }

        if (!File.Exists(fullPath))
        {
            throw new ToolExecutionException("File does not exist.", Protocol.JsonRpcError.InvalidParamsCode, new { path = fullPath });
        }

        RejectSymlinks(fullPath);
        return fullPath;
    }

    private bool IsPathUnderAllowedRoot(string fullPath)
    {
        var normalizedRoot = _options.AllowedRoot
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var rootWithSeparator = normalizedRoot + Path.DirectorySeparatorChar;

        return fullPath.Equals(normalizedRoot, _pathComparison) ||
               fullPath.StartsWith(rootWithSeparator, _pathComparison);
    }

    private static void RejectSymlinks(string fullPath)
    {
        var fileInfo = new FileInfo(fullPath);
        if (fileInfo.LinkTarget is not null)
        {
            throw new ToolExecutionException("Symlinks are not allowed for file paths.", Protocol.JsonRpcError.InvalidParamsCode);
        }

        var directory = fileInfo.Directory;
        while (directory is not null)
        {
            if (directory.LinkTarget is not null)
            {
                throw new ToolExecutionException("Symlinks are not allowed in directory path segments.", Protocol.JsonRpcError.InvalidParamsCode);
            }

            directory = directory.Parent;
        }
    }

    private void ValidateFile(string fullPath, IReadOnlySet<string> allowedExtensions, string fileType)
    {
        var extension = Path.GetExtension(fullPath).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
        {
            throw new ToolExecutionException(
                $"Unsupported {fileType} extension '{extension}'.",
                Protocol.JsonRpcError.InvalidParamsCode,
                new { allowed_extensions = allowedExtensions.OrderBy(x => x, StringComparer.Ordinal).ToArray() });
        }

        var fileInfo = new FileInfo(fullPath);
        if (fileInfo.Length > _options.MaxFileBytes)
        {
            throw new ToolExecutionException(
                $"{fileType} file exceeds configured size limit.",
                Protocol.JsonRpcError.InvalidParamsCode,
                new { max_file_mb = _options.MaxFileMb, actual_bytes = fileInfo.Length });
        }
    }
}
