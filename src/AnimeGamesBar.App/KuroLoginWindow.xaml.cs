using System.Text.Json;
using System.Text.RegularExpressions;
using AnimeGamesBar.App.Services.Skland;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AnimeGamesBar.App;

public sealed partial class KuroLoginWindow : Window
{
    private static readonly Uri LoginUri = new("https://www.kurobbs.com/mc/home/");
    private static readonly Regex JwtPattern = new(@"eyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+", RegexOptions.Compiled);

    private readonly TaskCompletionSource<KuroWebCredentialSnapshot?> _completion = new();
    private readonly SklandCredential _currentCredential;

    public KuroLoginWindow(SklandCredential currentCredential)
    {
        InitializeComponent();
        _currentCredential = currentCredential;

        Closed += OnClosed;
        _ = InitializeAsync();
    }

    public async Task<KuroWebCredentialSnapshot?> WaitForCredentialAsync(CancellationToken cancellationToken)
    {
        using var registration = cancellationToken.Register(() => _completion.TrySetCanceled(cancellationToken));
        return await _completion.Task;
    }

    private async Task InitializeAsync()
    {
        try
        {
            await LoginWebView.EnsureCoreWebView2Async();
            LoginWebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
            LoginWebView.CoreWebView2.NavigationCompleted += (_, args) =>
            {
                if (!args.IsSuccess)
                {
                    SetStatus("库街区页面加载失败，请检查网络后刷新。", InfoBarSeverity.Warning);
                    return;
                }

                SetStatus("请完成库街区登录。若页面已显示账号信息，点击“完成登录”。", InfoBarSeverity.Informational);
            };

            LoginWebView.Source = LoginUri;
        }
        catch (Exception ex)
        {
            SetStatus($"初始化库街区登录窗口失败：{ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void FinishButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var snapshot = await ExtractCredentialAsync();
            if (snapshot is null || !snapshot.HasAnySecret)
            {
                SetStatus("还没有读取到库街区 Token。请确认库街区页面已完成登录，再点一次“完成登录”。", InfoBarSeverity.Warning);
                return;
            }

            _completion.TrySetResult(snapshot);
            Close();
        }
        catch (Exception ex)
        {
            SetStatus($"读取库街区登录凭据失败：{ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void ReloadButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (LoginWebView.CoreWebView2 is null)
        {
            LoginWebView.Source = LoginUri;
            return;
        }

        LoginWebView.CoreWebView2.Reload();
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        _completion.TrySetResult(null);
        Close();
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _completion.TrySetResult(null);
    }

    private async Task<KuroWebCredentialSnapshot?> ExtractCredentialAsync()
    {
        if (LoginWebView.CoreWebView2 is null)
        {
            return null;
        }

        var script = """
(() => {
  const snapshot = {
    href: location.href,
    cookies: document.cookie || "",
    localStorage: {},
    sessionStorage: {}
  };

  for (let i = 0; i < localStorage.length; i += 1) {
    const key = localStorage.key(i);
    snapshot.localStorage[key] = localStorage.getItem(key);
  }

  for (let i = 0; i < sessionStorage.length; i += 1) {
    const key = sessionStorage.key(i);
    snapshot.sessionStorage[key] = sessionStorage.getItem(key);
  }

  return JSON.stringify(snapshot);
})()
""";

        var jsonLiteral = await LoginWebView.CoreWebView2.ExecuteScriptAsync(script);
        var snapshotJson = JsonSerializer.Deserialize<string>(jsonLiteral);
        if (string.IsNullOrWhiteSpace(snapshotJson))
        {
            return null;
        }

        var cookieText = await ReadBrowserCookiesAsync();
        using var document = JsonDocument.Parse(snapshotJson);
        var root = document.RootElement;
        var documentCookies = ReadString(root, "cookies");
        var allCookies = FirstNotBlank(
            string.Join("; ", new[] { cookieText, documentCookies }.Where(value => !string.IsNullOrWhiteSpace(value))),
            _currentCredential.Cookie);

        var token = FirstNotBlank(
            ExtractCookie(allCookies, "user_token"),
            ExtractCookie(allCookies, "token"),
            FindJwtByName(root, "user_token"),
            FindJwtByName(root, "token"),
            FindJwtByName(root, "accessToken"),
            FindJwtByName(root, "access_token"),
            FindFirstJwt(root),
            _currentCredential.Token);

        var userId = FirstNotBlank(
            FindValueByName(root, "userId"),
            FindValueByName(root, "user_id"),
            TryReadUserIdFromJwt(token),
            _currentCredential.UserId);

        var deviceId = FirstNotBlank(
            FindValueByName(root, "devCode"),
            FindValueByName(root, "devcode"),
            FindValueByName(root, "distinct_id"),
            _currentCredential.DeviceId,
            Guid.NewGuid().ToString("N"));

        return new KuroWebCredentialSnapshot(
            token ?? string.Empty,
            allCookies ?? string.Empty,
            userId ?? string.Empty,
            deviceId ?? Guid.NewGuid().ToString("N"));
    }

    private async Task<string> ReadBrowserCookiesAsync()
    {
        if (LoginWebView.CoreWebView2 is null)
        {
            return string.Empty;
        }

        var cookies = new List<string>();
        foreach (var uri in new[] { "https://www.kurobbs.com/", "https://api.kurobbs.com/", "https://web-static.kurobbs.com/" })
        {
            try
            {
                var items = await LoginWebView.CoreWebView2.CookieManager.GetCookiesAsync(uri);
                cookies.AddRange(items.Select(cookie => $"{cookie.Name}={cookie.Value}"));
            }
            catch
            {
            }
        }

        return string.Join("; ", cookies.Distinct(StringComparer.Ordinal));
    }

    private static string? FindJwtByName(JsonElement element, string name)
    {
        var value = FindValueByName(element, name);
        return FirstJwtIn(value);
    }

    private static string? FindFirstJwt(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var value = FindFirstJwt(property.Value);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    var value = FindFirstJwt(item);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }

                break;
            case JsonValueKind.String:
                return FirstJwtIn(element.GetString());
        }

        return null;
    }

    private static string? FirstJwtIn(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = JwtPattern.Match(text);
        return match.Success ? match.Value : null;
    }

    private static string? ExtractCookie(string? cookies, string name)
    {
        if (string.IsNullOrWhiteSpace(cookies))
        {
            return null;
        }

        foreach (var cookie in cookies.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var index = cookie.IndexOf('=');
            if (index <= 0)
            {
                continue;
            }

            var cookieName = cookie[..index].Trim();
            if (cookieName.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return cookie[(index + 1)..].Trim();
            }
        }

        return null;
    }

    private static string? TryReadUserIdFromJwt(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var parts = token.Split('.');
        if (parts.Length < 2)
        {
            return null;
        }

        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var document = JsonDocument.Parse(Convert.FromBase64String(payload));
            return FindValueByName(document.RootElement, "userId");
        }
        catch
        {
            return null;
        }
    }

    private static string? FindValueByName(JsonElement element, string name)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        var direct = ExtractString(property.Value);
                        if (!string.IsNullOrWhiteSpace(direct))
                        {
                            return direct;
                        }
                    }

                    var child = FindValueByName(property.Value, name);
                    if (!string.IsNullOrWhiteSpace(child))
                    {
                        return child;
                    }
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    var child = FindValueByName(item, name);
                    if (!string.IsNullOrWhiteSpace(child))
                    {
                        return child;
                    }
                }

                break;
            case JsonValueKind.String:
                var text = element.GetString();
                if (!string.IsNullOrWhiteSpace(text) && TryParseJson(text, out var parsed))
                {
                    using (parsed)
                    {
                        return FindValueByName(parsed.RootElement, name);
                    }
                }

                break;
        }

        return null;
    }

    private static string? ExtractString(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            return value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False
                ? value.ToString()
                : null;
        }

        return value.GetString();
    }

    private static string? ReadString(JsonElement element, string name)
    {
        return TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
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

    private static bool TryParseJson(string value, out JsonDocument document)
    {
        document = default!;
        var trimmed = value.Trim();
        if (trimmed.Length == 0 || (trimmed[0] != '{' && trimmed[0] != '['))
        {
            return false;
        }

        try
        {
            document = JsonDocument.Parse(trimmed);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? FirstNotBlank(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private void SetStatus(string message, InfoBarSeverity severity)
    {
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = severity;
        StatusInfoBar.IsOpen = true;
    }

    public sealed record KuroWebCredentialSnapshot(
        string Token,
        string Cookie,
        string UserId,
        string DeviceId)
    {
        public bool HasAnySecret => !string.IsNullOrWhiteSpace(Token) || !string.IsNullOrWhiteSpace(Cookie);
    }
}
