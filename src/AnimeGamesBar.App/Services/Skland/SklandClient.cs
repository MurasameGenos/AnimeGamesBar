using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AnimeGamesBar.App.Services;

namespace AnimeGamesBar.App.Services.Skland;

public sealed class SklandClient : ISklandClient
{
    private static readonly Uri BaseUri = new("https://zonai.skland.com");
    private const string RefreshPath = "/api/v1/auth/refresh";

    private readonly HttpClient _httpClient;
    private readonly IClock _clock;
    private readonly ISklandRequestSigner _signer;

    public SklandClient(HttpClient httpClient, IClock clock, ISklandRequestSigner signer)
    {
        _httpClient = httpClient;
        _clock = clock;
        _signer = signer;
    }

    public async Task<JsonDocument> GetJsonAsync(
        SklandCredential credential,
        string pathAndQuery,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        return await GetJsonAsync(credential, BaseUri, pathAndQuery, cancellationToken, headers);
    }

    public async Task<JsonDocument> GetJsonAsync(
        SklandCredential credential,
        Uri baseUri,
        string pathAndQuery,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        return await SendGetJsonAsync(credential, baseUri, pathAndQuery, cancellationToken, headers, allowRefresh: true);
    }

    private async Task<JsonDocument> SendGetJsonAsync(
        SklandCredential credential,
        Uri baseUri,
        string pathAndQuery,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? headers,
        bool allowRefresh)
    {
        var requestUri = new Uri(baseUri, pathAndQuery);
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

        ApplyHeaders(request, credential);
        ApplyExtraHeaders(request, headers);
        _signer.Sign(request, credential, _clock.Now);

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (allowRefresh && ShouldRefreshToken(body))
        {
            var refreshedToken = await RefreshTokenAsync(credential, cancellationToken);
            if (!string.IsNullOrWhiteSpace(refreshedToken))
            {
                var refreshedCredential = credential with { Token = refreshedToken };
                return await SendGetJsonAsync(refreshedCredential, baseUri, pathAndQuery, cancellationToken, headers, allowRefresh: false);
            }
        }

        if (!response.IsSuccessStatusCode)
        {
            throw CreateHttpException(response, body);
        }

        if (TryDecodeTransformedJson(body, out var decoded))
        {
            return decoded;
        }

        return JsonDocument.Parse(body);
    }

    private async Task<string?> RefreshTokenAsync(SklandCredential credential, CancellationToken cancellationToken)
    {
        using var document = await SendGetJsonAsync(
            credential with { Token = string.Empty },
            BaseUri,
            RefreshPath,
            cancellationToken,
            new Dictionary<string, string>(),
            allowRefresh: false);

        SklandApiGuard.ThrowIfError(document.RootElement);

        if (TryGetProperty(document.RootElement, "data", out var data) &&
            TryGetProperty(data, "token", out var token) &&
            token.ValueKind == JsonValueKind.String)
        {
            return token.GetString();
        }

        return null;
    }

    private static void ApplyHeaders(HttpRequestMessage request, SklandCredential credential)
    {
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.ParseAdd("Skland/1.32.1 (com.hypergryph.skland; build:103201004; Android 33; ) Okhttp/4.11.0");
        request.Headers.ConnectionClose = true;

        if (!string.IsNullOrWhiteSpace(credential.Cred))
        {
            request.Headers.TryAddWithoutValidation("cred", credential.Cred);
        }

        if (!string.IsNullOrWhiteSpace(credential.Cookie))
        {
            request.Headers.TryAddWithoutValidation("Cookie", credential.Cookie);
        }
    }

    private static void ApplyExtraHeaders(HttpRequestMessage request, IReadOnlyDictionary<string, string>? headers)
    {
        if (headers is null)
        {
            return;
        }

        foreach (var (name, value) in headers)
        {
            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(value))
            {
                request.Headers.TryAddWithoutValidation(name, value);
            }
        }
    }

    private static bool TryDecodeTransformedJson(string body, out JsonDocument decoded)
    {
        decoded = default!;

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(body);
        }
        catch (JsonException)
        {
            return false;
        }

        if (root is not JsonObject rootObject ||
            rootObject["data"] is not JsonObject dataObject ||
            dataObject["content"] is not JsonValue contentValue ||
            !contentValue.TryGetValue<string>(out var encoded) ||
            string.IsNullOrWhiteSpace(encoded))
        {
            return false;
        }

        JsonNode? transformedData;
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            transformedData = JsonNode.Parse(json);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }

        if (transformedData is null)
        {
            return false;
        }

        rootObject["data"] = transformedData;
        decoded = JsonDocument.Parse(rootObject.ToJsonString());
        return true;
    }

    private static bool ShouldRefreshToken(string body)
    {
        if (!TryReadCode(body, out var code))
        {
            return false;
        }

        return code is 10000 or 10003;
    }

    private static SklandApiException CreateHttpException(HttpResponseMessage response, string body)
    {
        var statusCode = (int)response.StatusCode;
        if (TryReadCode(body, out var code))
        {
            var message = TryReadMessage(body) ?? response.ReasonPhrase ?? "\u8BF7\u6C42\u5931\u8D25";
            return new SklandApiException(
                $"\u68EE\u7A7A\u5C9B\u8BF7\u6C42\u5931\u8D25\uFF1AHTTP {statusCode}, code {code}, {message}",
                statusCode,
                code);
        }

        return new SklandApiException(
            $"\u68EE\u7A7A\u5C9B\u8BF7\u6C42\u5931\u8D25\uFF1AHTTP {statusCode} {response.ReasonPhrase}",
            statusCode,
            null);
    }

    private static bool TryReadCode(string body, out int code)
    {
        code = 0;
        try
        {
            using var document = JsonDocument.Parse(body);
            if (!TryGetProperty(document.RootElement, "code", out var value))
            {
                return false;
            }

            return value.ValueKind switch
            {
                JsonValueKind.Number => value.TryGetInt32(out code),
                JsonValueKind.String => int.TryParse(value.GetString(), out code),
                _ => false
            };
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? TryReadMessage(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            foreach (var name in new[] { "message", "msg", "error", "reason" })
            {
                if (TryGetProperty(document.RootElement, name, out var value) &&
                    value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString();
                }
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        return false;
    }
}
