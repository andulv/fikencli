using System.Net;
using System.Text;
using System.Text.Json;
using FikenCli;

namespace FikenCli.Tests;

public sealed class FikenTransportTests
{
    [Fact]
    public async Task SendAsync_SendsSecurityHeadersAndPreservesCompactJson()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\n  \"name\": \"Ada\"\n}", Encoding.UTF8, "application/json")
        });
        using var client = CreateClient(handler);
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await new FikenTransport(client, () => "secret-value").SendAsync(
            HttpMethod.Get, "user", null, output, error, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal("{\"name\":\"Ada\"}" + Environment.NewLine, output.ToString());
        Assert.Empty(error.ToString());
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("secret-value", handler.AuthorizationParameter);
        Assert.True(Guid.TryParse(handler.RequestId, out _));
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task SendAsync_EmptySuccessWritesStatusAndLocation()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Created);
        response.Headers.Location = new Uri("https://api.fiken.no/api/v2/companies/demo/generalJournalEntries/42");
        using var client = CreateClient(new RecordingHandler(response));
        var output = new StringWriter();

        var exitCode = await new FikenTransport(client, () => "token").SendAsync(
            HttpMethod.Post, "resource", "{}", output, new StringWriter(), CancellationToken.None);

        Assert.Equal(0, exitCode);
        using var result = JsonDocument.Parse(output.ToString());
        Assert.Equal(201, result.RootElement.GetProperty("status").GetInt32());
        Assert.EndsWith("/42", result.RootElement.GetProperty("location").GetString());
    }

    [Fact]
    public async Task SendAsync_HttpFailureWritesStructuredErrorAndReturnsNonZero()
    {
        using var client = CreateClient(new RecordingHandler(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"message\":\"invalid\"}", Encoding.UTF8, "application/json")
        }));
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await new FikenTransport(client, () => "token").SendAsync(
            HttpMethod.Get, "user", null, output, error, CancellationToken.None);

        Assert.NotEqual(0, exitCode);
        Assert.Empty(output.ToString());
        using var result = JsonDocument.Parse(error.ToString());
        var value = result.RootElement.GetProperty("error");
        Assert.Equal(400, value.GetProperty("status").GetInt32());
        Assert.Equal("invalid", value.GetProperty("body").GetProperty("message").GetString());
        Assert.True(Guid.TryParse(value.GetProperty("requestId").GetString(), out _));
    }

    [Fact]
    public async Task NetworkCommand_MissingTokenWritesOnlyConfigurationError()
    {
        using var client = CreateClient(new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)));
        var output = new StringWriter();
        var error = new StringWriter();
        var app = new FikenCliApp(client, () => " ", output, error);

        var exitCode = await app.InvokeAsync(["user", "get"]);

        Assert.NotEqual(0, exitCode);
        Assert.Empty(output.ToString());
        Assert.Contains("FIKEN_API_TOKEN", error.ToString());
    }

    private static HttpClient CreateClient(HttpMessageHandler handler) => new(handler)
    {
        BaseAddress = FikenTransport.DefaultBaseAddress
    };

    private sealed class RecordingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }
        public string? RequestId { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            RequestId = request.Headers.GetValues("Fiken-Request-Id").Single();
            return Task.FromResult(response);
        }
    }
}
