using System.Net;
using System.Text;
using System.Text.Json;
using FikenCli;

namespace FikenCli.Tests;

public sealed class FikenWriteCommandTests
{
    private const string Payload = "{\n \"description\":\"Opening\",\n \"journalEntries\":[{\"date\":\"2026-01-01\",\"description\":\"Entry\",\"lines\":[{\"amount\":-100,\"debitAccount\":\"1500:10001\",\"debitVatCode\":0}]}]}";

    [Fact]
    public async Task Preview_EmitsExactTargetAndPayloadWithoutSending()
    {
        var handler = new RecordingHandler(HttpStatusCode.Created);
        var (app, output, error) = CreateApp(handler, Payload);

        var exitCode = await app.InvokeAsync(["general-journal-entries", "create", "--company-slug", "demo company", "--stdin"]);

        Assert.Equal(0, exitCode);
        Assert.Equal(0, handler.CallCount);
        Assert.Empty(error.ToString());
        using var preview = JsonDocument.Parse(output.ToString());
        Assert.Equal("POST", preview.RootElement.GetProperty("method").GetString());
        Assert.Equal("companies/demo%20company/generalJournalEntries", preview.RootElement.GetProperty("path").GetString());
        Assert.True(JsonElement.DeepEquals(JsonDocument.Parse(Payload).RootElement, preview.RootElement.GetProperty("payload")));
    }

    [Fact]
    public async Task Execute_PostsSameCompactPayloadExactlyOnce()
    {
        var handler = new RecordingHandler(HttpStatusCode.Created);
        var (app, output, error) = CreateApp(handler, Payload);

        var exitCode = await app.InvokeAsync(["general-journal-entries", "create", "--company-slug", "demo", "--stdin", "--execute"]);

        Assert.Equal(0, exitCode);
        Assert.Equal(1, handler.CallCount);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("companies/demo/generalJournalEntries", handler.RelativeTarget);
        Assert.Equal("application/json", handler.MediaType);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("token", handler.AuthorizationParameter);
        Assert.True(JsonElement.DeepEquals(JsonDocument.Parse(Payload).RootElement, JsonDocument.Parse(handler.Body!).RootElement));
        Assert.Empty(error.ToString());
        using var result = JsonDocument.Parse(output.ToString());
        Assert.Equal(201, result.RootElement.GetProperty("status").GetInt32());
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-json")]
    [InlineData("[]")]
    public async Task InvalidStdin_FailsWithoutSending(string payload)
    {
        var handler = new RecordingHandler(HttpStatusCode.Created);
        var (app, _, error) = CreateApp(handler, payload);

        var exitCode = await app.InvokeAsync(["general-journal-entries", "create", "--company-slug", "demo", "--stdin"]);

        Assert.NotEqual(0, exitCode);
        Assert.Equal(0, handler.CallCount);
        Assert.NotEmpty(error.ToString());
    }

    [Fact]
    public async Task MissingOrAmbiguousInput_FailsWithoutSending()
    {
        var file = Path.GetTempFileName();
        await File.WriteAllTextAsync(file, Payload);
        try
        {
            foreach (var args in new[]
            {
                new[] { "general-journal-entries", "create", "--company-slug", "demo" },
                new[] { "general-journal-entries", "create", "--company-slug", "demo", "--stdin", "--input", file }
            })
            {
                var handler = new RecordingHandler(HttpStatusCode.Created);
                var (app, _, _) = CreateApp(handler, Payload);
                Assert.NotEqual(0, await app.InvokeAsync(args));
                Assert.Equal(0, handler.CallCount);
            }
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ExecuteWithoutToken_FailsWithoutSending()
    {
        var handler = new RecordingHandler(HttpStatusCode.Created);
        var output = new StringWriter();
        var error = new StringWriter();
        var app = new FikenCliApp(CreateClient(handler), () => null, output, error, new StringReader(Payload));

        var exitCode = await app.InvokeAsync(["general-journal-entries", "create", "--company-slug", "demo", "--stdin", "--execute"]);

        Assert.NotEqual(0, exitCode);
        Assert.Equal(0, handler.CallCount);
        Assert.Empty(output.ToString());
    }

    [Fact]
    public async Task Execute_ApiRejectionRemainsFailure()
    {
        var handler = new RecordingHandler(HttpStatusCode.UnprocessableEntity, "{\"message\":\"unbalanced\"}");
        var (app, output, error) = CreateApp(handler, Payload);

        var exitCode = await app.InvokeAsync(["general-journal-entries", "create", "--company-slug", "demo", "--stdin", "--execute"]);

        Assert.NotEqual(0, exitCode);
        Assert.Equal(1, handler.CallCount);
        Assert.Empty(output.ToString());
        Assert.Contains("422", error.ToString());
        Assert.Contains("unbalanced", error.ToString());
    }

    private static (FikenCliApp App, StringWriter Output, StringWriter Error) CreateApp(RecordingHandler handler, string input)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        return (new FikenCliApp(CreateClient(handler), () => "token", output, error, new StringReader(input)), output, error);
    }

    private static HttpClient CreateClient(HttpMessageHandler handler) => new(handler) { BaseAddress = FikenTransport.DefaultBaseAddress };

    private sealed class RecordingHandler(HttpStatusCode status, string body = "") : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public HttpMethod? Method { get; private set; }
        public string? RelativeTarget { get; private set; }
        public string? MediaType { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            Method = request.Method;
            RelativeTarget = request.RequestUri!.PathAndQuery["/api/v2/".Length..];
            MediaType = request.Content?.Headers.ContentType?.MediaType;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            if (status == HttpStatusCode.Created) response.Headers.Location = new Uri("https://api.fiken.no/api/v2/companies/demo/generalJournalEntries/42");
            return response;
        }
    }
}
