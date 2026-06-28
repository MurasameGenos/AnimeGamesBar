using System.Text.Json;

namespace AnimeGamesBar.App.Services.Skland;

public static class SklandApiGuard
{
    public static void ThrowIfError(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new SklandApiException("\u68EE\u7A7A\u5C9B\u63A5\u53E3\u8FD4\u56DE\u683C\u5F0F\u5F02\u5E38\u3002");
        }

        var code = ReadCode(root);
        if (code is null or 0)
        {
            return;
        }

        var message = ReadString(root, "message", "msg", "error") ??
            $"\u68EE\u7A7A\u5C9B\u63A5\u53E3\u8FD4\u56DE\u9519\u8BEF\u7801 {code.Value}\u3002";
        throw new SklandApiException($"{message} (code {code.Value})", null, code);
    }

    private static int? ReadCode(JsonElement root)
    {
        if (!TryGetProperty(root, "code", out var code))
        {
            return null;
        }

        if (code.ValueKind == JsonValueKind.Number && code.TryGetInt32(out var value))
        {
            return value;
        }

        return code.ValueKind == JsonValueKind.String && int.TryParse(code.GetString(), out value)
            ? value
            : null;
    }

    private static string? ReadString(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetProperty(root, name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        value = default;

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
