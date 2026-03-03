using Microsoft.Extensions.Configuration;

namespace OnlineTeamTools.MCP.YouTube.YouTube;

public sealed class YouTubeOptions
{
    private const string DefaultAllowedRoot = "/Volumes/Data/Devs/Projects";

    public string ClientSecretsPath { get; init; } = string.Empty;

    public string RefreshToken { get; init; } = string.Empty;

    public string ApplicationName { get; init; } = "MCP.Youtube";

    public string AllowedRoot { get; init; } = Directory.GetCurrentDirectory();

    public int MaxFileMb { get; init; } = 2048;

    public long MaxFileBytes => MaxFileMb * 1024L * 1024L;

    public string DefaultPrivacy { get; init; } = "private";

    public int Concurrency { get; init; } = 1;

    public TimeSpan ToolTimeout { get; init; } = TimeSpan.FromMinutes(30);

    public IReadOnlySet<string> AllowedVideoExtensions { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4",
        ".mov"
    };

    public IReadOnlySet<string> AllowedImageExtensions { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png"
    };

    public string RedirectUri { get; init; } = "http://localhost:53682/callback";

    public static YouTubeOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var fallbackRoot = Directory.Exists(DefaultAllowedRoot)
            ? DefaultAllowedRoot
            : Directory.GetCurrentDirectory();

        var allowedRootInput = Read(configuration, "YOUTUBE_ALLOWED_ROOT", "YouTube:AllowedRoot", fallbackRoot);
        var allowedRoot = Path.GetFullPath(Path.IsPathRooted(allowedRootInput)
            ? allowedRootInput
            : Path.Combine(Directory.GetCurrentDirectory(), allowedRootInput));

        var maxFileMb = ReadInt(configuration, "YOUTUBE_MAX_FILE_MB", "YouTube:MaxFileMb", 2048, minimum: 1, maximum: 1024 * 100);
        var concurrency = ReadInt(configuration, "YOUTUBE_CONCURRENCY", "YouTube:Concurrency", 1, minimum: 1, maximum: 8);
        var timeoutSeconds = ReadInt(configuration, "YOUTUBE_TOOL_TIMEOUT_SECONDS", "YouTube:ToolTimeoutSeconds", 1800, minimum: 5, maximum: 24 * 3600);

        var defaultPrivacyRaw = Read(configuration, "YOUTUBE_DEFAULT_PRIVACY", "YouTube:DefaultPrivacy", "private");
        var defaultPrivacy = NormalizePrivacy(defaultPrivacyRaw);
        if (string.Equals(defaultPrivacy, "public", StringComparison.OrdinalIgnoreCase))
        {
            defaultPrivacy = "private";
        }

        return new YouTubeOptions
        {
            ClientSecretsPath = Read(configuration, "YOUTUBE_CLIENT_SECRETS_PATH", "YouTube:ClientSecretsPath", string.Empty),
            RefreshToken = Read(configuration, "YOUTUBE_REFRESH_TOKEN", "YouTube:RefreshToken", string.Empty),
            ApplicationName = Read(configuration, "YOUTUBE_APPLICATION_NAME", "YouTube:ApplicationName", "MCP.Youtube"),
            AllowedRoot = allowedRoot,
            MaxFileMb = maxFileMb,
            DefaultPrivacy = defaultPrivacy,
            Concurrency = concurrency,
            ToolTimeout = TimeSpan.FromSeconds(timeoutSeconds),
            AllowedVideoExtensions = ParseExtensions(Read(configuration, "YOUTUBE_ALLOWED_VIDEO_EXTENSIONS", "YouTube:AllowedVideoExtensions", ".mp4,.mov"), new[] { ".mp4", ".mov" }),
            AllowedImageExtensions = ParseExtensions(Read(configuration, "YOUTUBE_ALLOWED_IMAGE_EXTENSIONS", "YouTube:AllowedImageExtensions", ".jpg,.jpeg,.png"), new[] { ".jpg", ".jpeg", ".png" }),
            RedirectUri = Read(configuration, "YOUTUBE_REDIRECT_URI", "YouTube:RedirectUri", "http://localhost:53682/callback")
        };
    }

    private static string Read(IConfiguration configuration, string envKey, string appSettingsKey, string fallback)
    {
        var value = configuration[envKey] ?? configuration[appSettingsKey];
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static int ReadInt(IConfiguration configuration, string envKey, string appSettingsKey, int fallback, int minimum, int maximum)
    {
        var raw = Read(configuration, envKey, appSettingsKey, fallback.ToString());

        if (!int.TryParse(raw, out var parsed))
        {
            return fallback;
        }

        return Math.Clamp(parsed, minimum, maximum);
    }

    private static string NormalizePrivacy(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "private" or "unlisted" or "public"
            ? normalized
            : "private";
    }

    private static IReadOnlySet<string> ParseExtensions(string rawValue, IEnumerable<string> fallback)
    {
        var parsed = rawValue
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.StartsWith('.') ? x : $".{x}")
            .Select(x => x.ToLowerInvariant())
            .Where(x => x.Length >= 2)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (parsed.Count == 0)
        {
            parsed = fallback.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        return parsed;
    }
}
