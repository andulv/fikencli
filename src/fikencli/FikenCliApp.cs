using System.CommandLine;

namespace FikenCli;

public sealed class FikenCliApp
{
    private readonly RootCommand _rootCommand;
    private readonly TextWriter _output;
    private readonly TextWriter _error;

    public FikenCliApp(HttpClient httpClient, Func<string?> tokenProvider, TextWriter output, TextWriter error)
    {
        _output = output;
        _error = error;
        var transport = new FikenTransport(httpClient, tokenProvider);
        _rootCommand = new RootCommand("Direct, machine-oriented access to the Fiken API v2.");

        var user = new Command("user", "Operations for the authenticated Fiken user.");
        user.Subcommands.Add(FikenCommandFactory.CreateGetCommand(
            "get",
            "Get the authenticated user (GET /user).",
            _ => "user",
            transport,
            output,
            error));
        _rootCommand.Subcommands.Add(user);
    }

    public async Task<int> InvokeAsync(string[] args, CancellationToken cancellationToken = default)
    {
        try
        {
            var configuration = new InvocationConfiguration
            {
                Output = _output,
                Error = _error,
                EnableDefaultExceptionHandler = false
            };
            return await _rootCommand.Parse(args).InvokeAsync(configuration, cancellationToken);
        }
        catch (FikenConfigurationException exception)
        {
            await WriteExceptionAsync("configuration", exception.Message);
            return 2;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await WriteExceptionAsync("cancelled", "The operation was cancelled.");
            return 130;
        }
        catch (Exception exception)
        {
            await WriteExceptionAsync("unexpected", exception.Message);
            return 1;
        }
    }

    private Task WriteExceptionAsync(string type, string message) =>
        _error.WriteLineAsync(System.Text.Json.JsonSerializer.Serialize(new
        {
            error = new { type, message }
        }));
}
