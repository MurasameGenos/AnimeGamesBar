namespace AnimeGamesBar.App.Services.Skland;

public sealed class DefaultSklandRequestSigner : ISklandRequestSigner
{
    public void Sign(HttpRequestMessage request, SklandCredential credential, DateTimeOffset timestamp, string body = "")
    {
        var signTimestamp = timestamp.ToUnixTimeSeconds().ToString();
        var platform = "1";
        var deviceId = string.IsNullOrWhiteSpace(credential.DeviceId)
            ? Guid.NewGuid().ToString("N")
            : credential.DeviceId;
        var versionName = "1.1.0";

        request.Headers.TryAddWithoutValidation("platform", platform);
        request.Headers.TryAddWithoutValidation("timestamp", signTimestamp);
        request.Headers.TryAddWithoutValidation("dId", deviceId);
        request.Headers.TryAddWithoutValidation("vName", versionName);

        if (request.RequestUri is null)
        {
            return;
        }

        var path = request.RequestUri.AbsolutePath;
        if (string.IsNullOrWhiteSpace(credential.Token) && !path.EndsWith("/auth/refresh", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var bodyOrQuery = request.Method == HttpMethod.Get
            ? request.RequestUri.Query.TrimStart('?')
            : body;
        var headerJson = $"{{\"platform\":\"{platform}\",\"timestamp\":\"{signTimestamp}\",\"dId\":\"{deviceId}\",\"vName\":\"{versionName}\"}}";
        var textToSign = path + bodyOrQuery + signTimestamp + headerJson;
        var hmacHex = HmacSha256Hex(credential.Token ?? string.Empty, textToSign);
        var sign = Md5Hex(hmacHex);

        request.Headers.TryAddWithoutValidation("sign", sign);
    }

    private static string HmacSha256Hex(string key, string value)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private static string Md5Hex(string value)
    {
        return Convert.ToHexString(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
    }
}
