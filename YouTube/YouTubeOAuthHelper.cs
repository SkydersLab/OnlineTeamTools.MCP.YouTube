namespace OnlineTeamTools.MCP.YouTube.YouTube;

public static class YouTubeOAuthHelper
{
    public static string BuildAuthorizationUrl(YouTubeOptions options)
    {
        var flow = YouTubeClientFactory.CreateAuthorizationFlow(options);
        var request = flow.CreateAuthorizationCodeRequest(options.RedirectUri);
        var uri = request.Build();
        var parameters = ParseQuery(uri.Query);

        parameters["access_type"] = "offline";
        parameters["prompt"] = "consent";

        return BuildUrl(uri, parameters);
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

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var trimmed = query.TrimStart('?');

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return result;
        }

        var pairs = trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in pairs)
        {
            var separatorIndex = pair.IndexOf('=');
            if (separatorIndex < 0)
            {
                var keyOnly = Uri.UnescapeDataString(pair);
                if (!string.IsNullOrWhiteSpace(keyOnly))
                {
                    result[keyOnly] = string.Empty;
                }

                continue;
            }

            var key = Uri.UnescapeDataString(pair[..separatorIndex]);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var value = Uri.UnescapeDataString(pair[(separatorIndex + 1)..]);
            result[key] = value;
        }

        return result;
    }

    private static string BuildUrl(Uri uri, IReadOnlyDictionary<string, string> parameters)
    {
        var query = string.Join("&", parameters.Select(x =>
            $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}"));

        var builder = new UriBuilder(uri)
        {
            Query = query
        };

        return builder.Uri.AbsoluteUri;
    }
}
