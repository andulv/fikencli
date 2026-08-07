using System.Net;
using System.Text;
using FikenCli;

namespace FikenCli.Tests;

public sealed class FikenReadCommandTests
{
    [Fact]
    public async Task AccountBalanceList_MapsRequiredDateFiltersAndPagination()
    {
        var handler = new RecordingHandler();
        var (app, output, error) = CreateApp(handler);

        var exitCode = await app.InvokeAsync([
            "account-balances", "list", "--company-slug", "demo company", "--date", "2026-08-01",
            "--from-account", "1000", "--to-account", "2999", "--page", "2", "--page-size", "50"]);

        Assert.True(exitCode == 0, error.ToString());
        Assert.Empty(error.ToString());
        Assert.Equal(1, handler.CallCount);
        Assert.Equal("companies/demo%20company/accountBalances?date=2026-08-01&fromAccount=1000&toAccount=2999&page=2&pageSize=50", handler.RelativeTarget);
        Assert.Equal("[]" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public async Task AccountGet_EscapesSubaccountCodeInPath()
    {
        var handler = new RecordingHandler();
        var (app, _, _) = CreateApp(handler);

        var exitCode = await app.InvokeAsync(["accounts", "get", "--company-slug", "demo", "--account-code", "1500:10001"]);

        Assert.Equal(0, exitCode);
        Assert.Equal("companies/demo/accounts/1500%3A10001", handler.RelativeTarget);
    }

    [Theory]
    [InlineData("account-balances", "list", "--company-slug", "demo")]
    [InlineData("account-balances", "list", "--company-slug", "demo", "--date", "08/01/2026")]
    [InlineData("companies", "list", "--page", "-1")]
    [InlineData("journal-entries", "get", "--company-slug", "demo", "--journal-entry-id", "0")]
    public async Task InvalidRequiredValues_FailBeforeHttp(params string[] args)
    {
        var handler = new RecordingHandler();
        var (app, _, error) = CreateApp(handler);

        var exitCode = await app.InvokeAsync(args);

        Assert.NotEqual(0, exitCode);
        Assert.Equal(0, handler.CallCount);
        Assert.NotEmpty(error.ToString());
    }

    [Fact]
    public async Task JournalEntryList_ForwardsOnlySuppliedFilters()
    {
        var handler = new RecordingHandler();
        var (app, _, error) = CreateApp(handler);

        var exitCode = await app.InvokeAsync([
            "journal-entries", "list", "--company-slug", "demo", "--date-ge", "2026-01-01", "--date-lt", "2026-02-01"]);

        Assert.True(exitCode == 0, error.ToString());
        Assert.Equal("companies/demo/journalEntries?dateLt=2026-02-01&dateGe=2026-01-01", handler.RelativeTarget);
        Assert.Equal(1, handler.CallCount);
    }

    private static (FikenCliApp App, StringWriter Output, StringWriter Error) CreateApp(RecordingHandler handler)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var client = new HttpClient(handler) { BaseAddress = FikenTransport.DefaultBaseAddress };
        return (new FikenCliApp(client, () => "token", output, error), output, error);
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
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            });
        }
    }
}
