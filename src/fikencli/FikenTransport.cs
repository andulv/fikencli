using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FikenCli;

public sealed class FikenTransport(
    HttpClient httpClient,
    Func<string?> tokenProvider,
    IFikenRequestPolicy? requestPolicy = null)
{
    public static readonly Uri DefaultBaseAddress = new("https://api.fiken.no/api/v2/");

    public void Authorize(HttpMethod method, string relativePath) =>
        (requestPolicy ?? AllowAllFikenRequestPolicy.Instance).Authorize(method, relativePath);

    public async Task<int> SendAsync(
        HttpMethod method,
        string relativePath,
        string? jsonBody,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        Authorize(method, relativePath);

        var token = tokenProvider();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new FikenConfigurationException("FIKEN_API_TOKEN is required and must not be blank.");
        }

        var requestId = Guid.NewGuid().ToString();
        using var request = new HttpRequestMessage(method, relativePath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("Fiken-Request-Id", requestId);
        if (jsonBody is not null)
        {
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        }

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            await error.WriteLineAsync(CreateErrorJson((int)response.StatusCode, requestId, body));
            return 1;
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            var location = response.Headers.Location?.ToString();
            await output.WriteLineAsync(JsonSerializer.Serialize(new
            {
                status = (int)response.StatusCode,
                location
            }));
            return 0;
        }

        using var document = JsonDocument.Parse(body);
        await output.WriteLineAsync(JsonSerializer.Serialize(document.RootElement));
        return 0;
    }

    private static string CreateErrorJson(int status, string requestId, string body)
    {
        object? bodyValue;
        try
        {
            using var document = JsonDocument.Parse(body);
            bodyValue = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            bodyValue = body;
        }

        return JsonSerializer.Serialize(new
        {
            error = new
            {
                status,
                requestId,
                body = bodyValue
            }
        });
    }
}

public sealed class FikenConfigurationException(string message) : Exception(message);
