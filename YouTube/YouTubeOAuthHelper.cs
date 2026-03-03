namespace OnlineTeamTools.MCP.YouTube.YouTube;

public static class YouTubeOAuthHelper
{
    public static string BuildAuthorizationUrl(YouTubeOptions options)
    {
        var flow = YouTubeClientFactory.CreateAuthorizationFlow(options);
        var request = flow.CreateAuthorizationCodeRequest(options.RedirectUri);
        var baseUrl = request.Build().AbsoluteUri;
        var separator = baseUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{baseUrl}{separator}access_type=offline&prompt=consent";
    }

    public static async Task<string> ExchangeCodeForRefreshTokenAsync(YouTubeOptions options, string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Authorization code is required.", nameof(code));
        }

        var flow = YouTubeClientFactory.CreateAuthorizationFlow(options);
        var token = await flow.ExchangeCodeForTokenAsync(
                userId: "mcp-youtube-cli",
                code: code,
                redirectUri: options.RedirectUri,
                taskCancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(token.RefreshToken))
        {
            throw new InvalidOperationException("No refresh token was returned. Ensure consent prompt was forced and offline access requested.");
        }

        return token.RefreshToken;
    }
}
