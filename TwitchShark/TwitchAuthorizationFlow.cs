using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

internal sealed class TwitchAuthorizationFlow
{
    private const string AuthorizeUrl = "https://id.twitch.tv/oauth2/authorize";
    private const string CallbackPath = "/twitch-shark/oauth";
    private const int CallbackPort = 37081;
    private static readonly TimeSpan AuthorizationTimeout = TimeSpan.FromMinutes(3);
    private static readonly string[] RequiredScopes = { "chat:read", "chat:edit" };
    private const string FragmentBridgeHtml = "<!DOCTYPE html><html><head><meta charset='utf-8'><title>Twitch Authorization</title></head><body><p>Completing authorization...</p><script>const hash=window.location.hash;if(hash&&hash.length>1){const query=hash.substring(1);const target=window.location.pathname+'?'+query;window.location.replace(target);}else{document.body.innerHTML='<p>Missing authorization data. You can close this tab and try again.</p>';}</script></body></html>";

    private readonly string clientId;

    public TwitchAuthorizationFlow(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new ArgumentException("Client ID is required", nameof(clientId));
        }

        this.clientId = clientId.Trim();
    }

    public async Task<TwitchAuthorizationResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        var state = GenerateState();
        var listener = StartCallbackListener(out var port);
        var redirectUri = BuildRedirectUri(port);

        try
        {
            var authorizeUrl = BuildAuthorizeUrl(state, redirectUri);
            Application.OpenURL(authorizeUrl);
            var tokenResponse = await WaitForImplicitTokenAsync(listener, state, cancellationToken).ConfigureAwait(false);
            return new TwitchAuthorizationResult
            {
                AccessToken = tokenResponse.AccessToken,
                RefreshToken = "",
                AccessTokenExpiresAt = tokenResponse.ExpiresIn > 0 ? DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn) : (DateTime?)null
            };
        }
        finally
        {
            listener.Stop();
        }
    }

    private TcpListener StartCallbackListener(out int port)
    {
        try
        {
            var listener = new TcpListener(IPAddress.Loopback, CallbackPort);
            listener.Start();
            port = CallbackPort;
            return listener;
        }
        catch (SocketException ex)
        {
            var message = $"Unable to start the local Twitch authorization callback on port {CallbackPort}. Please allow \"Twitch Shark OAuth\" through Windows Defender or ensure no other application is bound to that port.";
            throw new InvalidOperationException(message, ex);
        }
    }

    private async Task<ImplicitTokenResponse> WaitForImplicitTokenAsync(TcpListener listener, string expectedState, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var acceptTask = listener.AcceptTcpClientAsync();
            var completed = await Task.WhenAny(acceptTask, Task.Delay(AuthorizationTimeout, cancellationToken)).ConfigureAwait(false);

            if (completed != acceptTask)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException("Timed out waiting for Twitch authorization.");
            }

            using (var client = await acceptTask.ConfigureAwait(false))
            {
                var request = await ReadHttpRequestAsync(client).ConfigureAwait(false);

                if (request == null)
                {
                    continue;
                }

                if (!request.Path.Equals(CallbackPath, StringComparison.OrdinalIgnoreCase))
                {
                    await SendBrowserResponseAsync(client.GetStream(), HttpStatusCode.NotFound, BuildHtml("Not Found", "Incorrect redirect target.")).ConfigureAwait(false);
                    continue;
                }

                if (request.Query.Count == 0)
                {
                    await SendBrowserResponseAsync(client.GetStream(), HttpStatusCode.OK, FragmentBridgeHtml).ConfigureAwait(false);
                    continue;
                }

                if (request.Query.TryGetValue("error", out var error))
                {
                    var description = request.Query.TryGetValue("error_description", out var desc) ? desc : error;
                    await SendBrowserResponseAsync(client.GetStream(), HttpStatusCode.BadRequest, BuildHtml("Authorization failed", description ?? "Authorization failed.")).ConfigureAwait(false);
                    throw new InvalidOperationException(description ?? "Authorization failed.");
                }

                if (!request.Query.TryGetValue("state", out var stateValue) || !string.Equals(stateValue, expectedState, StringComparison.Ordinal))
                {
                    await SendBrowserResponseAsync(client.GetStream(), HttpStatusCode.BadRequest, BuildHtml("Authorization failed", "State mismatch. Please try again.")).ConfigureAwait(false);
                    throw new InvalidOperationException("Authorization state mismatch.");
                }

                if (!request.Query.TryGetValue("access_token", out var accessToken) || string.IsNullOrWhiteSpace(accessToken))
                {
                    const string missingToken = "Authorization response did not include an access token.";
                    await SendBrowserResponseAsync(client.GetStream(), HttpStatusCode.BadRequest, BuildHtml("Authorization failed", missingToken)).ConfigureAwait(false);
                    throw new InvalidOperationException(missingToken);
                }

                var expiresIn = 0;
                if (request.Query.TryGetValue("expires_in", out var expiresRaw))
                {
                    int.TryParse(expiresRaw, out expiresIn);
                }

                await SendBrowserResponseAsync(client.GetStream(), HttpStatusCode.OK, BuildHtml("Authorization complete", "You can close this browser tab and return to Raft.")).ConfigureAwait(false);

                return new ImplicitTokenResponse
                {
                    AccessToken = accessToken,
                    ExpiresIn = expiresIn
                };
            }
        }
    }

    private async Task<HttpRequestData> ReadHttpRequestAsync(TcpClient client)
    {
        var stream = client.GetStream();
        using (var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true))
        {
            var requestLine = await reader.ReadLineAsync().ConfigureAwait(false);

            if (string.IsNullOrEmpty(requestLine))
            {
                return null;
            }

            var parts = requestLine.Split(' ');

            if (parts.Length < 2)
            {
                return null;
            }

            var target = parts[1];
            var path = target;
            var query = "";
            var queryIndex = target.IndexOf('?');

            if (queryIndex >= 0)
            {
                path = target.Substring(0, queryIndex);
                query = target.Substring(queryIndex + 1);
            }

            await DrainHeadersAsync(reader).ConfigureAwait(false);

            return new HttpRequestData
            {
                Path = path,
                Query = ParseQueryString(query)
            };
        }
    }

    private static async Task DrainHeadersAsync(StreamReader reader)
    {
        string line;

        do
        {
            line = await reader.ReadLineAsync().ConfigureAwait(false);
        }
        while (!string.IsNullOrEmpty(line));
    }

    private static Dictionary<string, string> ParseQueryString(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(query))
        {
            return result;
        }

        var trimmed = query.TrimStart('?');
        var parts = trimmed.Split('&');

        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part))
            {
                continue;
            }

            var kvp = part.Split(new[] { '=' }, 2);
            var key = Uri.UnescapeDataString(kvp[0]);
            var value = kvp.Length > 1 ? Uri.UnescapeDataString(kvp[1]) : "";
            result[key] = value;
        }

        return result;
    }

    private static async Task SendBrowserResponseAsync(Stream stream, HttpStatusCode statusCode, string body)
    {
        var statusText = $"{(int)statusCode} {statusCode}";
        var payload = $"HTTP/1.1 {statusText}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}";
        var buffer = Encoding.UTF8.GetBytes(payload);
        await stream.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
        await stream.FlushAsync().ConfigureAwait(false);
    }

    private static string BuildHtml(string title, string message)
    {
        var safeTitle = WebUtility.HtmlEncode(title);
        var safeMessage = WebUtility.HtmlEncode(message);

        return $"<html><head><title>{safeTitle}</title><style>body{{font-family:sans-serif;background:#0F1116;color:#E3E6EC;padding:40px;}}</style></head><body><h2>{safeTitle}</h2><p>{safeMessage}</p></body></html>";
    }

    private static string GenerateState()
    {
        var buffer = new byte[16];

        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(buffer);
        }

        return Base64UrlEncode(buffer);
    }

    private string BuildAuthorizeUrl(string state, string redirectUri)
    {
        var scope = Uri.EscapeDataString(string.Join(" ", RequiredScopes));
        return $"{AuthorizeUrl}?client_id={Uri.EscapeDataString(clientId)}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=token&scope={scope}&state={Uri.EscapeDataString(state)}&force_verify=true";
    }

    private static string BuildRedirectUri(int port)
    {
        return $"http://localhost:{port}{CallbackPath}";
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private class HttpRequestData
    {
        public string Path { get; set; }
        public Dictionary<string, string> Query { get; set; }
    }

    private class ImplicitTokenResponse
    {
        public string AccessToken { get; set; }
        public int ExpiresIn { get; set; }
    }
}

internal sealed class TwitchAuthorizationResult
{
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public DateTime? AccessTokenExpiresAt { get; set; }
}
