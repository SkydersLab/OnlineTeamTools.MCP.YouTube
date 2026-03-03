using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using OnlineTeamTools.MCP.YouTube.Infrastructure;
using OnlineTeamTools.MCP.YouTube.Tools;

namespace OnlineTeamTools.MCP.YouTube.YouTube;

public sealed class YouTubeClientFactory
{
    private static readonly string[] Scopes =
    {
        YouTubeService.Scope.Youtube,
        YouTubeService.Scope.YoutubeUpload
    };

    private readonly YouTubeOptions _options;
    private readonly StderrLogger _logger;
    private readonly SemaphoreSlim _credentialLock = new(1, 1);

    private UserCredential? _credential;

    public YouTubeClientFactory(YouTubeOptions options, StderrLogger logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<YouTubeService> CreateAsync(CancellationToken cancellationToken)
    {
        var credential = await GetCredentialAsync(cancellationToken).ConfigureAwait(false);

        return new YouTubeService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = _options.ApplicationName
        });
    }

    private async Task<UserCredential> GetCredentialAsync(CancellationToken cancellationToken)
    {
        if (_credential is not null)
        {
            return _credential;
        }

        await _credentialLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_credential is not null)
            {
                return _credential;
            }

            if (string.IsNullOrWhiteSpace(_options.ClientSecretsPath))
            {
                throw new ToolExecutionException("YOUTUBE_CLIENT_SECRETS_PATH is not configured.");
            }

            if (string.IsNullOrWhiteSpace(_options.RefreshToken))
            {
                throw new ToolExecutionException("YOUTUBE_REFRESH_TOKEN is not configured.");
            }

            if (!File.Exists(_options.ClientSecretsPath))
            {
                throw new ToolExecutionException(
                    "Client secrets file was not found.",
                    Protocol.JsonRpcError.ServerErrorCode,
                    new { path = _options.ClientSecretsPath });
            }

            await using var stream = File.OpenRead(_options.ClientSecretsPath);
            var clientSecrets = GoogleClientSecrets.FromStream(stream).Secrets;

            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = clientSecrets,
                Scopes = Scopes
            });

            var token = new TokenResponse
            {
                RefreshToken = _options.RefreshToken
            };

            var credential = new UserCredential(flow, "mcp-youtube", token);
            var refreshed = await credential.RefreshTokenAsync(cancellationToken).ConfigureAwait(false);
            if (!refreshed)
            {
                throw new ToolExecutionException("Failed to refresh access token using provided refresh token.");
            }

            _credential = credential;
            _logger.Info("YouTube OAuth credential initialized");
            return credential;
        }
        finally
        {
            _credentialLock.Release();
        }
    }

    internal static GoogleAuthorizationCodeFlow CreateAuthorizationFlow(YouTubeOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ClientSecretsPath))
        {
            throw new ToolExecutionException("YOUTUBE_CLIENT_SECRETS_PATH is required for auth helper mode.");
        }

        if (!File.Exists(options.ClientSecretsPath))
        {
            throw new ToolExecutionException(
                "Client secrets file was not found.",
                Protocol.JsonRpcError.ServerErrorCode,
                new { path = options.ClientSecretsPath });
        }

        using var stream = File.OpenRead(options.ClientSecretsPath);
        var clientSecrets = GoogleClientSecrets.FromStream(stream).Secrets;

        return new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = clientSecrets,
            Scopes = Scopes
        });
    }
}
