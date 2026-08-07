using System.Net;
using System.Text;
using FikenCli;

namespace FikenCli.Tests;

public sealed class FikenReusableApiTests
{
    [Fact]
    public async Task CommandString_RunsExistingReadCommandInProcess()
    {
        var handler = new RecordingHandler();
        var output = new StringWriter();
        var error = new StringWriter();
        var app = CreateApp(handler, output, error, new StringReader(string.Empty), new FikenHttpMethodPolicy(HttpMethod.Get));

        var exitCode = await app.InvokeAsync("accounts get --company-slug demo --account-code 1500:10001");

        Assert.Equal(0, exitCode);
        Assert.Equal("companies/demo/accounts/1500%3A10001", handler.RelativeTarget);
        Assert.Equal("{}" + Environment.NewLine, output.ToString());
        Assert.Empty(error.ToString());
    }

    [Fact]
    public async Task GetOnlyPolicy_RejectsExecutingWriteBeforeTransport()
    {
        var handler = new RecordingHandler();
        var output = new StringWriter();
        var error = new StringWriter();
        var app = CreateApp(handler, output, error, new StringReader("{}"), new FikenHttpMethodPolicy(HttpMethod.Get));

        var exitCode = await app.InvokeAsync("general-journal-entries create --company-slug demo --stdin --execute");

        Assert.Equal(3, exitCode);
        Assert.Equal(0, handler.CallCount);
        Assert.Empty(output.ToString());
        Assert.Contains("not permitted", error.ToString());
    }

    [Fact]
    public async Task GetOnlyPolicy_RejectsWritePreviewBecauseItIsAWriteOperation()
    {
        var handler = new RecordingHandler();
        var output = new StringWriter();
        var error = new StringWriter();
        var app = CreateApp(handler, output, error, new StringReader("{}"), new FikenHttpMethodPolicy(HttpMethod.Get));

        var exitCode = await app.InvokeAsync("general-journal-entries create --company-slug demo --stdin");

        Assert.Equal(3, exitCode);
        Assert.Equal(0, handler.CallCount);
        Assert.Empty(output.ToString());
        Assert.Contains("not permitted", error.ToString());
    }

    private static FikenCliApp CreateApp(
        HttpMessageHandler handler,
        TextWriter output,
        TextWriter error,
        TextReader input,
        IFikenRequestPolicy policy)
    {
        var client = new HttpClient(handler) { BaseAddress = FikenTransport.DefaultBaseAddress };
        return new FikenCliApp(client, () => "token", output, error, input, policy);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public string? RelativeTarget { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            RelativeTarget = request.RequestUri!.PathAndQuery["/api/v2/".Length..];
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });
        }
    }
}
