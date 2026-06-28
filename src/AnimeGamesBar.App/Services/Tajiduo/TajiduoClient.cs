using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AnimeGamesBar.App.Models;
using AnimeGamesBar.App.Services.Kuro;
using AnimeGamesBar.App.Services.Skland;

namespace AnimeGamesBar.App.Services.Tajiduo;

public sealed class TajiduoClient
{
    public const string YihuanGameId = "1289";
    public const string YihuanCommunityId = "2";
    private const string LaohuBaseUrl = "https://user.laohu.com";
    private const string TajiduoBaseUrl = "https://bbs-api.tajiduo.com";
    private const string LaohuAppId = "10550";
    private const string LaohuAppKey = "89155cc4e8634ec5b1b6364013b23e3e";
    private const string TajiduoUserCenterAppId = "10551";
    private const string TajiduoAppVersion = "1.2.4";
    private const string TajiduoUserAgent = "okhttp/4.12.0";
    private const string LaohuUserAgent = "okhttp/4.9.0";
    private const string TajiduoDsSalt = "pUds3dfMkl";
    private const string DeviceModel = "Pixel 6";
    private const string DeviceSystem = "Android 14";
    private const string DeviceType = "Pixel 6";
    private const string PackageName = "com.pwrd.htassistant";
    private const string VersionCode = "12";
    private const string SdkVersion = "4.273.0";

    private readonly HttpClient _httpClient;

    public TajiduoClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task SendSmsCodeAsync(string mobile, string deviceId, CancellationToken cancellationToken)
    {
        var form = CreateLaohuCommonForm(deviceId, secondsTimestamp: true);
        form["cellphone"] = mobile;
        form["areaCodeId"] = "1";
        form["type"] = "16";
        form["sign"] = GenerateLaohuSign(form);

        using var document = await PostLaohuFormAsync(
            "/m/newApi/sendPhoneCaptchaWithOutLogin",
            form,
            cancellationToken);

        ThrowIfLaohuError(document.RootElement, "发送验证码失败");
    }

    public async Task<SklandCredential> LoginBySmsCodeAsync(
        SklandCredential currentCredential,
        string mobile,
        string code,
        string deviceId,
        CancellationToken cancellationToken)
    {
        var form = CreateLaohuCommonForm(deviceId, secondsTimestamp: false);
        form["idfa"] = string.Empty;
        form["mac"] = string.Empty;
        form["version"] = VersionCode;
        form.Remove("versionCode");
        form.Remove("imei");
        form["cellphone"] = EncryptLaohuValue(mobile);
        form["captcha"] = EncryptLaohuValue(code);
        form["areaCodeId"] = "1";
        form["type"] = "16";
        form["sign"] = GenerateLaohuSign(form);

        using var document = await PostLaohuFormAsync("/openApi/sms/new/login", form, cancellationToken);
        ThrowIfLaohuError(document.RootElement, "老虎账号登录失败");

        var result = KuroClient.Get(document.RootElement, "result");
        var laohuToken = KuroClient.ReadString(result, "token") ?? string.Empty;
        var laohuUserId = KuroClient.ReadString(result, "userId", "user_id") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(laohuToken) || string.IsNullOrWhiteSpace(laohuUserId))
        {
            throw new TajiduoApiException("老虎账号登录成功，但没有返回 token 或 userId。");
        }

        return await UserCenterLoginAsync(currentCredential, laohuToken, laohuUserId, deviceId, cancellationToken);
    }

    public async Task<SklandCredential> UserCenterLoginAsync(
        SklandCredential currentCredential,
        string laohuToken,
        string laohuUserId,
        string deviceId,
        CancellationToken cancellationToken)
    {
        using var document = await SendTajiduoAsync(
            HttpMethod.Post,
            "/usercenter/api/login",
            new Dictionary<string, string>
            {
                ["token"] = laohuToken,
                ["userIdentity"] = laohuUserId,
                ["appId"] = TajiduoUserCenterAppId
            },
            currentCredential with { DeviceId = deviceId },
            cancellationToken,
            authorization: string.Empty,
            uid: "0");

        var data = KuroClient.Get(document.RootElement, "data");
        var accessToken = KuroClient.ReadString(data, "accessToken", "access_token") ?? string.Empty;
        var refreshToken = KuroClient.ReadString(data, "refreshToken", "refresh_token") ?? string.Empty;
        var uid = KuroClient.ReadString(data, "uid", "userId") ?? currentCredential.UserId;
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new TajiduoApiException("塔吉多登录成功，但没有返回 Access Token。");
        }

        return new SklandCredential(
            $"{laohuUserId}|{laohuToken}",
            accessToken.Trim(),
            refreshToken.Trim(),
            uid?.Trim() ?? string.Empty,
            EnsureDeviceId(deviceId),
            DateTimeOffset.Now);
    }

    public async Task<IReadOnlyList<ArknightsPlayerBinding>> GetYihuanBindingsAsync(
        SklandCredential credential,
        CancellationToken cancellationToken)
    {
        var roles = new List<ArknightsPlayerBinding>();
        await AddBoundRoleAsync(roles, credential, cancellationToken);
        await AddGameRolesAsync(roles, credential, cancellationToken);

        return roles
            .GroupBy(role => role.Uid)
            .Select(group => group.First())
            .ToArray();
    }

    public async Task<YihuanAccountStatus> GetYihuanStatusAsync(
        SklandCredential credential,
        ArknightsPlayerBinding player,
        CancellationToken cancellationToken)
    {
        using var document = await SendTajiduoAsync(
            HttpMethod.Get,
            $"/apihub/awapi/yh/roleHome?roleId={Uri.EscapeDataString(player.Uid)}",
            null,
            credential,
            cancellationToken);

        var data = KuroClient.Get(document.RootElement, "data");
        var signState = await GetGameSignStateAsync(credential, cancellationToken);
        return new YihuanAccountStatus(
            KuroClient.ReadString(data, "rolename", "roleName") ?? player.NickName,
            KuroClient.ReadString(data, "servername", "serverName") ?? player.ServerName,
            new ResourceMeter(
                KuroClient.ReadInt(data, "staminaValue") ?? 0,
                KuroClient.ReadInt(data, "staminaMaxValue") ?? 0,
                null),
            new ResourceMeter(
                KuroClient.ReadInt(data, "citystaminaValue", "cityStaminaValue") ?? 0,
                KuroClient.ReadInt(data, "citystaminaMaxValue", "cityStaminaMaxValue") ?? 0,
                null),
            new ProgressStatus(
                KuroClient.ReadInt(data, "dayvalue", "dayValue") ?? 0,
                100),
            new ProgressStatus(
                KuroClient.ReadInt(data, "weekcopiesremainCnt", "weekCopiesRemainCnt") ?? 0,
                3),
            signState,
            DateTimeOffset.Now);
    }

    public async Task<bool> IsAppSignedInAsync(SklandCredential credential, CancellationToken cancellationToken)
    {
        using var document = await SendTajiduoAsync(
            HttpMethod.Get,
            $"/apihub/api/getSignState?communityId={YihuanCommunityId}",
            null,
            credential,
            cancellationToken);

        var data = KuroClient.Get(document.RootElement, "data");
        return IsTruthy(data);
    }

    public async Task AppSignInAsync(SklandCredential credential, CancellationToken cancellationToken)
    {
        await SendTajiduoAsync(
            HttpMethod.Post,
            "/apihub/api/signin",
            new Dictionary<string, string> { ["communityId"] = YihuanCommunityId },
            credential,
            cancellationToken);
    }

    public async Task<bool> GetGameSignStateAsync(SklandCredential credential, CancellationToken cancellationToken)
    {
        using var document = await SendTajiduoAsync(
            HttpMethod.Get,
            $"/apihub/awapi/signin/state?gameId={YihuanGameId}",
            null,
            credential,
            cancellationToken);

        var data = KuroClient.Get(document.RootElement, "data");
        return IsTruthy(KuroClient.Get(data, "todaySign"));
    }

    public async Task GameSignInAsync(
        SklandCredential credential,
        ArknightsPlayerBinding binding,
        CancellationToken cancellationToken)
    {
        await SendTajiduoAsync(
            HttpMethod.Post,
            "/apihub/awapi/sign",
            new Dictionary<string, string>
            {
                ["roleId"] = binding.Uid,
                ["gameId"] = YihuanGameId
            },
            credential,
            cancellationToken);
    }

    public static string EnsureDeviceId(string? currentDeviceId)
    {
        if (!string.IsNullOrWhiteSpace(currentDeviceId))
        {
            return currentDeviceId.Trim();
        }

        return $"HT{Guid.NewGuid():N}".ToUpperInvariant()[..16];
    }

    private async Task AddBoundRoleAsync(
        List<ArknightsPlayerBinding> roles,
        SklandCredential credential,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(credential.UserId))
        {
            return;
        }

        try
        {
            using var document = await SendTajiduoAsync(
                HttpMethod.Get,
                $"/apihub/api/getGameBindRole?uid={Uri.EscapeDataString(credential.UserId)}&gameId={YihuanGameId}",
                null,
                credential,
                cancellationToken);

            AddRoleFromElement(roles, KuroClient.Get(document.RootElement, "data"), credential);
        }
        catch
        {
            // The full role list endpoint below usually still works if no default role is bound.
        }
    }

    private async Task AddGameRolesAsync(
        List<ArknightsPlayerBinding> roles,
        SklandCredential credential,
        CancellationToken cancellationToken)
    {
        using var document = await SendTajiduoAsync(
            HttpMethod.Get,
            $"/usercenter/api/v2/getGameRoles?gameId={YihuanGameId}",
            null,
            credential,
            cancellationToken);

        var data = KuroClient.Get(document.RootElement, "data");
        if (data.ValueKind != JsonValueKind.Array)
        {
            AddRoleFromElement(roles, data, credential);
            return;
        }

        foreach (var role in data.EnumerateArray())
        {
            AddRoleFromElement(roles, role, credential);
        }
    }

    private static void AddRoleFromElement(
        List<ArknightsPlayerBinding> roles,
        JsonElement role,
        SklandCredential credential)
    {
        if (role.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var roleId = KuroClient.ReadString(role, "roleId", "roleid", "uid");
        var roleName = KuroClient.ReadString(role, "roleName", "rolename", "name", "nickName") ?? roleId;
        if (string.IsNullOrWhiteSpace(roleId) || string.IsNullOrWhiteSpace(roleName))
        {
            return;
        }

        var serverId = KuroClient.ReadString(role, "serverId", "serverid") ?? string.Empty;
        var serverName = KuroClient.ReadString(role, "serverName", "servername") ?? serverId;
        roles.Add(new ArknightsPlayerBinding(
            roleId,
            credential.UserId,
            roleName,
            serverName,
            string.IsNullOrWhiteSpace(serverName) ? "异环" : serverName,
            serverId,
            "yihuan",
            roleId));
    }

    private async Task<JsonDocument> SendTajiduoAsync(
        HttpMethod method,
        string pathAndQuery,
        IReadOnlyDictionary<string, string>? form,
        SklandCredential credential,
        CancellationToken cancellationToken,
        string? authorization = null,
        string? uid = null)
    {
        var uri = new Uri(new Uri(TajiduoBaseUrl), pathAndQuery);
        using var request = new HttpRequestMessage(method, uri);
        ApplyTajiduoHeaders(
            request,
            credential,
            authorization ?? credential.Token,
            uid ?? (string.IsNullOrWhiteSpace(credential.UserId) ? "0" : credential.UserId));

        if (form is not null)
        {
            request.Content = new FormUrlEncodedContent(form);
        }

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new TajiduoApiException($"塔吉多请求失败：HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        var document = JsonDocument.Parse(body);
        ThrowIfTajiduoError(document.RootElement);
        return document;
    }

    private async Task<JsonDocument> PostLaohuFormAsync(
        string path,
        IReadOnlyDictionary<string, string> form,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(LaohuBaseUrl), path));
        request.Headers.UserAgent.ParseAdd(LaohuUserAgent);
        request.Content = new FormUrlEncodedContent(form);

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new TajiduoApiException($"老虎登录请求失败：HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        return JsonDocument.Parse(body);
    }

    private static Dictionary<string, string> CreateLaohuCommonForm(string deviceId, bool secondsTimestamp)
    {
        var timestamp = secondsTimestamp
            ? DateTimeOffset.Now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)
            : DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);

        return new Dictionary<string, string>
        {
            ["appId"] = LaohuAppId,
            ["channelId"] = "1",
            ["deviceId"] = deviceId,
            ["deviceType"] = DeviceType,
            ["deviceModel"] = DeviceModel,
            ["deviceName"] = DeviceModel,
            ["deviceSys"] = DeviceSystem,
            ["adm"] = deviceId,
            ["idfa"] = string.Empty,
            ["sdkVersion"] = SdkVersion,
            ["bid"] = PackageName,
            ["t"] = timestamp,
            ["versionCode"] = VersionCode,
            ["imei"] = string.Empty
        };
    }

    private static void ApplyTajiduoHeaders(
        HttpRequestMessage request,
        SklandCredential credential,
        string authorization,
        string uid)
    {
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.ParseAdd(TajiduoUserAgent);
        request.Headers.TryAddWithoutValidation("platform", "android");
        request.Headers.TryAddWithoutValidation("deviceid", EnsureDeviceId(credential.DeviceId));
        request.Headers.TryAddWithoutValidation("appversion", TajiduoAppVersion);
        request.Headers.TryAddWithoutValidation("uid", uid);
        request.Headers.TryAddWithoutValidation("authorization", authorization ?? string.Empty);
        request.Headers.TryAddWithoutValidation("ds", GenerateDs());
    }

    private static string GenerateLaohuSign(IReadOnlyDictionary<string, string> parameters)
    {
        var builder = new StringBuilder();
        foreach (var key in parameters.Keys.OrderBy(static key => key, StringComparer.Ordinal))
        {
            builder.Append(parameters[key]);
        }

        builder.Append(LaohuAppKey);
        return Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }

    private static string EncryptLaohuValue(string value)
    {
        var key = Encoding.UTF8.GetBytes(LaohuAppKey[^16..]);
        using var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;

        using var encryptor = aes.CreateEncryptor();
        var plaintext = Encoding.UTF8.GetBytes(value);
        var encrypted = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);
        return Convert.ToBase64String(encrypted);
    }

    private static string GenerateDs()
    {
        const string nonceSource = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        Span<char> nonce = stackalloc char[8];
        for (var index = 0; index < nonce.Length; index++)
        {
            nonce[index] = nonceSource[RandomNumberGenerator.GetInt32(nonceSource.Length)];
        }

        var timestamp = DateTimeOffset.Now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var raw = $"{timestamp}{nonce.ToString()}{TajiduoAppVersion}{TajiduoDsSalt}";
        var hash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
        return $"{timestamp},{nonce},{hash}";
    }

    private static void ThrowIfLaohuError(JsonElement root, string fallback)
    {
        var code = KuroClient.ReadInt(root, "code");
        if (code is null or 0)
        {
            return;
        }

        var message = KuroClient.ReadString(root, "message", "msg") ?? fallback;
        throw new TajiduoApiException($"{fallback}：code {code}, {message}", code);
    }

    private static void ThrowIfTajiduoError(JsonElement root)
    {
        var code = KuroClient.ReadInt(root, "code");
        if (code is null or 0)
        {
            return;
        }

        var message = KuroClient.ReadString(root, "msg", "message") ?? "请求失败";
        throw new TajiduoApiException($"塔吉多请求失败：code {code}, {message}", code);
    }

    private static bool IsTruthy(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.Number => value.TryGetInt32(out var number) && number != 0,
            JsonValueKind.String => bool.TryParse(value.GetString(), out var boolean)
                ? boolean
                : value.GetString() is "1" or "true",
            _ => false
        };
    }
}
