using System.Net.Http.Headers;
using System.Text.Json;
using AnimeGamesBar.App.Services;

namespace AnimeGamesBar.App.Services.Skland;

public sealed class SklandClient : ISklandClient
{
    private static readonly Uri BaseUri = new("https://zonai.skland.com");

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
        CancellationToken cancellationToken)
    {
        var requestUri = new Uri(BaseUri, pathAndQuery);
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

        ApplyHeaders(request, credential);
        _signer.Sign(request, credential, _clock.Now);

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new SklandApiException($"\u68EE\u7A7A\u5C9B\u8BF7\u6C42\u5931\u8D25\uFF1A{(int)response.StatusCode} {response.ReasonPhrase}");
        }

        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static void ApplyHeaders(HttpRequestMessage request, SklandCredential credential)
    {
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.ParseAdd("Skland/1.0 AnimeGamesBar/0.1");

        if (!string.IsNullOrWhiteSpace(credential.Cred))
        {
            request.Headers.TryAddWithoutValidation("cred", credential.Cred);
        }

        if (!string.IsNullOrWhiteSpace(credential.Token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.Token);
            request.Headers.TryAddWithoutValidation("token", credential.Token);
        }

        if (!string.IsNullOrWhiteSpace(credential.Cookie))
        {
            request.Headers.TryAddWithoutValidation("Cookie", credential.Cookie);
        }
    }
}
