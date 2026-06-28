using System.Net.Http.Headers;
using System.Text.Json;
using AnimeGamesBar.App.Services.Skland;

namespace AnimeGamesBar.App.Services.Kuro;

public sealed class KuroMobileLoginClient
{
    private static readonly Uri BaseUri = new("https://api.kurobbs.com");
    private const string UserAgent = "okhttp/3.11.0";
    private readonly HttpClient _httpClient;

    public KuroMobileLoginClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<KuroSmsCodeResult> SendSmsCodeAsync(
        string mobile,
        string deviceId,
        CancellationToken cancellationToken)
    {
        using var document = await PostFormAsync(
            "/user/getSmsCode",
            deviceId,
            new Dictionary<string, string>
            {
                ["mobile"] = mobile,
                ["geeTestData"] = string.Empty
            },
            cancellationToken);

        var data = KuroClient.Get(document.RootElement, "data");
        var geeTestRequired = data.ValueKind == JsonValueKind.Object
            && KuroClient.Get(data, "geeTest").ValueKind == JsonValueKind.True;

        return new KuroSmsCodeResult(geeTestRequired);
    }

    public async Task<SklandCredential> LoginBySmsCodeAsync(
        SklandCredential currentCredential,
        string mobile,
        string code,
        string deviceId,
        CancellationToken cancellationToken)
    {
        using var document = await PostFormAsync(
            "/user/sdkLogin",
            deviceId,
            new Dictionary<string, string>
            {
                ["mobile"] = mobile,
                ["code"] = code,
                ["devCode"] = deviceId,
                ["gameList"] = string.Empty
            },
            cancellationToken);

        var data = KuroClient.Get(document.RootElement, "data");
        var token = KuroClient.ReadString(data, "token");
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new KuroApiException("库街区登录成功但没有返回 APP Token。");
        }

        var userId = KuroClient.ReadString(data, "userId", "user_id") ?? currentCredential.UserId;
        var cookie = $"user_token={token}";

        return new SklandCredential(
            currentCredential.Cred,
            token.Trim(),
            cookie,
            userId?.Trim() ?? string.Empty,
            deviceId.Trim(),
            DateTimeOffset.Now);
    }

    public static string EnsureDeviceId(string? currentDeviceId)
    {
        return string.IsNullOrWhiteSpace(currentDeviceId)
            ? Guid.NewGuid().ToString("N")
            : currentDeviceId.Trim();
    }

    private async Task<JsonDocument> PostFormAsync(
        string path,
        string deviceId,
        IReadOnlyDictionary<string, string> form,
        CancellationToken cancellationToken)
    {
        var requestUri = new Uri(BaseUri, path);
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        ApplyMobileHeaders(request, deviceId);
        request.Content = new FormUrlEncodedContent(form);

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new KuroApiException($"库街区登录请求失败：HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        var document = JsonDocument.Parse(body);
        ThrowIfError(document.RootElement);
        return document;
    }

    private static void ApplyMobileHeaders(HttpRequestMessage request, string deviceId)
    {
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("osVersion", "Android");
        request.Headers.TryAddWithoutValidation("devCode", deviceId);
        request.Headers.TryAddWithoutValidation("distinct_id", deviceId);
        request.Headers.TryAddWithoutValidation("countryCode", "CN");
        request.Headers.TryAddWithoutValidation("source", "android");
        request.Headers.TryAddWithoutValidation("lang", "zh-Hans");
        request.Headers.TryAddWithoutValidation("version", "2.2.0");
        request.Headers.TryAddWithoutValidation("versionCode", "2200");
        request.Headers.TryAddWithoutValidation("channelId", "2");
        request.Headers.TryAddWithoutValidation("model", "23127PN0CC");
        request.Headers.UserAgent.ParseAdd(UserAgent);
    }

    private static void ThrowIfError(JsonElement root)
    {
        var code = KuroClient.ReadInt(root, "code");
        if (code is null or 200)
        {
            return;
        }

        var message = KuroClient.ReadString(root, "msg", "message") ?? "请求失败";
        throw new KuroApiException($"库街区登录请求失败：code {code}, {message}", code);
    }
}

public sealed record KuroSmsCodeResult(bool GeeTestRequired);
