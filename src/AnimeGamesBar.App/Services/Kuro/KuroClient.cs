using System.Net.Http.Headers;
using System.Text.Json;
using AnimeGamesBar.App.Services.Skland;

namespace AnimeGamesBar.App.Services.Kuro;

public sealed class KuroClient
{
    private static readonly Uri BaseUri = new("https://api.kurobbs.com");
    private const string UserAgent = "okhttp/3.11.0";
    private readonly HttpClient _httpClient;

    public KuroClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<JsonDocument> PostFormAsync(
        SklandCredential credential,
        string path,
        IReadOnlyDictionary<string, string> form,
        CancellationToken cancellationToken,
        bool browserLike = false)
    {
        var requestUri = new Uri(BaseUri, path);
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        ApplyHeaders(request, credential, browserLike);
        request.Content = new FormUrlEncodedContent(form);

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new KuroApiException($"库街区请求失败：HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        var document = JsonDocument.Parse(body);
        ThrowIfError(document.RootElement);
        return document;
    }

    private static void ApplyHeaders(HttpRequestMessage request, SklandCredential credential, bool browserLike)
    {
        var token = credential.Token;
        var deviceId = string.IsNullOrWhiteSpace(credential.DeviceId)
            ? Guid.NewGuid().ToString("N")
            : credential.DeviceId;

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("token", token);
        request.Headers.TryAddWithoutValidation("Cookie", $"user_token={token}");
        request.Headers.TryAddWithoutValidation("source", "android");
        request.Headers.TryAddWithoutValidation("devCode", deviceId);
        request.Headers.TryAddWithoutValidation("devcode", deviceId);
        request.Headers.TryAddWithoutValidation("osVersion", "Android");
        request.Headers.TryAddWithoutValidation("countryCode", "CN");
        request.Headers.TryAddWithoutValidation("model", "23127PN0CC");
        request.Headers.TryAddWithoutValidation("lang", "zh-Hans");
        request.Headers.TryAddWithoutValidation("version", "2.2.0");
        request.Headers.TryAddWithoutValidation("versionCode", "2200");
        request.Headers.UserAgent.ParseAdd(UserAgent);

        if (browserLike)
        {
            request.Headers.TryAddWithoutValidation("origin", "https://web-static.kurobbs.com");
            request.Headers.TryAddWithoutValidation("referer", "https://web-static.kurobbs.com/");
            request.Headers.TryAddWithoutValidation("sec-fetch-site", "same-site");
            request.Headers.TryAddWithoutValidation("sec-fetch-mode", "cors");
            request.Headers.TryAddWithoutValidation("sec-fetch-dest", "empty");
            request.Headers.TryAddWithoutValidation("devcode", $"{deviceId}, Mozilla/5.0 KuroGameBox/2.2.0");
        }
    }

    private static void ThrowIfError(JsonElement root)
    {
        var code = ReadInt(root, "code");
        if (code is null or 200)
        {
            return;
        }

        var message = ReadString(root, "msg", "message") ?? "请求失败";
        throw new KuroApiException($"库街区请求失败：code {code}, {message}", code);
    }

    public static JsonElement Get(JsonElement root, params string[] names)
    {
        var current = root;
        foreach (var name in names)
        {
            if (current.ValueKind != JsonValueKind.Object)
            {
                return default;
            }

            var found = false;
            foreach (var property in current.EnumerateObject())
            {
                if (!property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                current = property.Value;
                found = true;
                break;
            }

            if (!found)
            {
                return default;
            }
        }

        return current;
    }

    public static string? ReadString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            var value = Get(element, name);
            if (value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            if (value.ValueKind == JsonValueKind.Number)
            {
                return value.GetRawText();
            }
        }

        return null;
    }

    public static int? ReadInt(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            var value = Get(element, name);
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number))
            {
                return number;
            }
        }

        return null;
    }

    public static long? ReadLong(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            var value = Get(element, name);
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out number))
            {
                return number;
            }
        }

        return null;
    }
}
