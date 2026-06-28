using System.Text.Json;
using AnimeGamesBar.App.Services.Skland;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AnimeGamesBar.App;

public sealed partial class SklandLoginWindow : Window
{
    private static readonly Uri LoginUri = new("https://www.skland.com/");

    private readonly TaskCompletionSource<SklandWebCredentialSnapshot?> _completion = new();
    private readonly SklandCredential _currentCredential;

    public SklandLoginWindow(SklandCredential currentCredential)
    {
        InitializeComponent();
        _currentCredential = currentCredential;

        Closed += OnClosed;
        _ = InitializeAsync();
    }

    public async Task<SklandWebCredentialSnapshot?> WaitForCredentialAsync(CancellationToken cancellationToken)
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
                    SetStatus("\u68EE\u7A7A\u5C9B\u9875\u9762\u52A0\u8F7D\u5931\u8D25\uFF0C\u8BF7\u68C0\u67E5\u7F51\u7EDC\u540E\u5237\u65B0\u3002", InfoBarSeverity.Warning);
                    return;
                }

                SetStatus("\u8BF7\u5B8C\u6210\u767B\u5F55\u3002\u82E5\u9875\u9762\u5DF2\u663E\u793A\u5934\u50CF\u6216\u8D26\u53F7\u4FE1\u606F\uFF0C\u70B9\u51FB\u201C\u5B8C\u6210\u767B\u5F55\u201D\u3002", InfoBarSeverity.Informational);
            };

            LoginWebView.Source = LoginUri;
        }
        catch (Exception ex)
        {
            SetStatus($"\u521D\u59CB\u5316\u767B\u5F55\u7A97\u53E3\u5931\u8D25\uFF1A{ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void FinishButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var snapshot = await ExtractCredentialAsync();
            if (snapshot is null || !snapshot.HasAnySecret)
            {
                SetStatus("\u8FD8\u6CA1\u6709\u8BFB\u53D6\u5230\u767B\u5F55\u51ED\u636E\u3002\u8BF7\u786E\u8BA4\u68EE\u7A7A\u5C9B\u9875\u9762\u5DF2\u5B8C\u6210\u767B\u5F55\uFF0C\u518D\u70B9\u4E00\u6B21\u201C\u5B8C\u6210\u767B\u5F55\u201D\u3002", InfoBarSeverity.Warning);
                return;
            }

            _completion.TrySetResult(snapshot);
            Close();
        }
        catch (Exception ex)
        {
            SetStatus($"\u8BFB\u53D6\u767B\u5F55\u51ED\u636E\u5931\u8D25\uFF1A{ex.Message}", InfoBarSeverity.Error);
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

    private async Task<SklandWebCredentialSnapshot?> ExtractCredentialAsync()
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

        using var document = JsonDocument.Parse(snapshotJson);
        var root = document.RootElement;

        var cred = FirstNotBlank(
            ExtractNamedValue(ReadStorageValue(root, "SK_OAUTH_CRED_KEY"), "cred"),
            ExtractNamedValue(ReadStorageValue(root, "SK_ADMIN_CRED_KEY"), "cred"),
            ReadStorageValue(root, "SK_OAUTH_CRED_KEY"),
            ReadStorageValue(root, "SK_ADMIN_CRED_KEY"),
            FindValueByName(root, "cred"));

        var token = FirstNotBlank(
            ExtractNamedValue(ReadStorageValue(root, "SK_TOKEN_CACHE_KEY"), "token"),
            ReadStorageValue(root, "SK_TOKEN_CACHE_KEY"),
            _currentCredential.Token);

        var deviceId = FirstNotBlank(
            ReadDeviceId(root),
            FindValueByName(root, "dId"),
            _currentCredential.DeviceId,
            Guid.NewGuid().ToString("N"));

        var userId = FirstNotBlank(
            FindValueByName(root, "userId"),
            _currentCredential.UserId);

        var cookie = FirstNotBlank(
            ReadString(root, "cookies"),
            _currentCredential.Cookie);

        return new SklandWebCredentialSnapshot(
            cred ?? string.Empty,
            token ?? string.Empty,
            cookie ?? string.Empty,
            userId ?? string.Empty,
            deviceId ?? Guid.NewGuid().ToString("N"));
    }

    private static string? ReadStorageValue(JsonElement root, string key)
    {
        return FirstNotBlank(
            ReadStorageValue(root, "localStorage", key),
            ReadStorageValue(root, "sessionStorage", key));
    }

    private static string? ReadStorageValue(JsonElement root, string storageName, string key)
    {
        if (!TryGetProperty(root, storageName, out var storage) || storage.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!TryGetProperty(storage, key, out var value))
        {
            return null;
        }

        return ExtractString(value);
    }

    private static string? ReadDeviceId(JsonElement root)
    {
        var raw = ReadStorageValue(root, "SK_SHUMEI_DEVICE_ID_KEY");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (TryParseJson(raw, out var parsed))
        {
            using (parsed)
            {
                return FindValueByName(parsed.RootElement, "id");
            }
        }

        return raw;
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

        var text = value.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (TryParseJson(text, out var parsed))
        {
            using (parsed)
            {
                return FirstNotBlank(
                    FindValueByName(parsed.RootElement, "cred"),
                    FindValueByName(parsed.RootElement, "token"),
                    FindValueByName(parsed.RootElement, "id"),
                    text);
            }
        }

        return text;
    }

    private static string? ExtractNamedValue(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || !TryParseJson(value, out var parsed))
        {
            return null;
        }

        using (parsed)
        {
            return FindValueByName(parsed.RootElement, name);
        }
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
}
