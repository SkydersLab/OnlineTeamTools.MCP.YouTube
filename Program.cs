using System.Text.Json;
using Microsoft.Extensions.Configuration;
using OnlineTeamTools.MCP.YouTube.Infrastructure;
using OnlineTeamTools.MCP.YouTube.Jobs;
using OnlineTeamTools.MCP.YouTube.Protocol;
using OnlineTeamTools.MCP.YouTube.Tools;
using OnlineTeamTools.MCP.YouTube.YouTube;

namespace OnlineTeamTools.MCP.YouTube;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        LoadDotEnvIfPresent();

        var configuration = BuildConfiguration();
        var options = YouTubeOptions.FromConfiguration(configuration);

        if (args.Length > 0)
        {
            return await HandleAuthHelperModeAsync(args, options).ConfigureAwait(false);
        }

        var logger = new StderrLogger();
        var pathSafetyValidator = new PathSafetyValidator(options);
        var uploadConcurrencyGate = new UploadConcurrencyGate(options);
        var youTubeClientFactory = new YouTubeClientFactory(options, logger);
        var youTubeUploader = new YouTubeUploader(youTubeClientFactory, pathSafetyValidator, uploadConcurrencyGate);
        var youTubeVideoReader = new YouTubeVideoReader(youTubeClientFactory);
        var uploadJobManager = new UploadJobManager(youTubeUploader, logger);
        var dispatcher = new ToolDispatcher(logger, options, youTubeUploader, youTubeVideoReader, uploadJobManager);

        logger.Info("Server starting", ("allowed_root", options.AllowedRoot), ("concurrency", options.Concurrency));

        using var shutdownCts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdownCts.Cancel();
        };

        while (!shutdownCts.IsCancellationRequested)
        {
            var line = await Console.In.ReadLineAsync().ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            JsonRpcResponse response;

            try
            {
                using var document = JsonDocument.Parse(line);
                response = await dispatcher.DispatchAsync(document.RootElement.Clone(), shutdownCts.Token).ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                logger.Warn("Invalid JSON request", ("error", ex.Message));
                response = JsonRpcResponse.FromError(null, JsonRpcError.ParseError("Invalid JSON payload."));
            }
            catch (Exception ex)
            {
                logger.Error("Unhandled server exception", ("error", ex.Message));
                response = JsonRpcResponse.FromError(null, JsonRpcError.InternalError("Unexpected server error."));
            }

            var payload = JsonSerializer.Serialize(response, JsonRpcSerialization.Options);
            await Console.Out.WriteLineAsync(payload).ConfigureAwait(false);
            await Console.Out.FlushAsync().ConfigureAwait(false);
        }

        logger.Info("Server shutting down");
        return 0;
    }

    private static async Task<int> HandleAuthHelperModeAsync(string[] args, YouTubeOptions options)
    {
        if (string.Equals(args[0], "--print-auth-url", StringComparison.OrdinalIgnoreCase))
        {
            var authUrl = YouTubeOAuthHelper.BuildAuthorizationUrl(options);
            Console.WriteLine(authUrl);
            return 0;
        }

        if (string.Equals(args[0], "--exchange-code", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
            {
                Console.Error.WriteLine("Missing authorization code. Usage: --exchange-code <CODE>");
                return 1;
            }

            var refreshToken = await YouTubeOAuthHelper.ExchangeCodeForRefreshTokenAsync(options, args[1], CancellationToken.None)
                .ConfigureAwait(false);

            Console.WriteLine(refreshToken);
            return 0;
        }

        Console.Error.WriteLine("Unknown arguments. Supported helper modes: --print-auth-url, --exchange-code <CODE>");
        return 1;
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();
    }

    private static void LoadDotEnvIfPresent()
    {
        foreach (var path in GetDotEnvCandidates())
        {
            if (!File.Exists(path))
            {
                continue;
            }

            foreach (var line in File.ReadLines(path))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
                {
                    continue;
                }

                var separatorIndex = trimmed.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                var key = trimmed[..separatorIndex].Trim();
                var value = trimmed[(separatorIndex + 1)..].Trim();

                if (value.Length >= 2 &&
                    ((value.StartsWith('"') && value.EndsWith('"')) ||
                     (value.StartsWith('\'') && value.EndsWith('\''))))
                {
                    value = value[1..^1];
                }

                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                {
                    Environment.SetEnvironmentVariable(key, value);
                }
            }
        }
    }

    private static IReadOnlyList<string> GetDotEnvCandidates()
    {
        var candidates = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        static void AddCandidate(HashSet<string> seenSet, List<string> list, string path)
        {
            var fullPath = Path.GetFullPath(path);
            if (seenSet.Add(fullPath))
            {
                list.Add(fullPath);
            }
        }

        var currentDirectory = Directory.GetCurrentDirectory();
        AddCandidate(seen, candidates, Path.Combine(currentDirectory, ".env"));

        var baseDirectory = AppContext.BaseDirectory;
        AddCandidate(seen, candidates, Path.Combine(baseDirectory, ".env"));

        var directory = new DirectoryInfo(baseDirectory);
        for (var depth = 0; depth < 8 && directory is not null; depth++)
        {
            AddCandidate(seen, candidates, Path.Combine(directory.FullName, ".env"));
            directory = directory.Parent;
        }

        return candidates;
    }
}
