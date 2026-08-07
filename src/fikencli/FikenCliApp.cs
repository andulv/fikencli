using System.CommandLine;

namespace FikenCli;

public sealed class FikenCliApp
{
    private readonly RootCommand _rootCommand;
    private readonly TextWriter _output;
    private readonly TextWriter _error;

    public FikenCliApp(
        HttpClient httpClient,
        Func<string?> tokenProvider,
        TextWriter output,
        TextWriter error,
        TextReader? input = null,
        IFikenRequestPolicy? requestPolicy = null)
    {
        _output = output;
        _error = error;
        var transport = new FikenTransport(httpClient, tokenProvider, requestPolicy);
        _rootCommand = new RootCommand("Direct, machine-oriented access to the Fiken API v2.");
        FikenReadCommands.AddTo(_rootCommand, transport, output, error);
        FikenWriteCommands.AddTo(_rootCommand, transport, input ?? TextReader.Null, output, error);
    }

    public Task<int> InvokeAsync(string commandLine, CancellationToken cancellationToken = default) =>
        InvokeAsync(_rootCommand.Parse(commandLine), cancellationToken);

    public Task<int> InvokeAsync(string[] args, CancellationToken cancellationToken = default) =>
        InvokeAsync(_rootCommand.Parse(args), cancellationToken);

    private async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        try
        {
            var configuration = new InvocationConfiguration
            {
                Output = _output,
                Error = _error,
                EnableDefaultExceptionHandler = false
            };
            return await parseResult.InvokeAsync(configuration, cancellationToken);
        }
        catch (FikenConfigurationException exception)
        {
            await WriteExceptionAsync("configuration", exception.Message);
            return 2;
        }
        catch (FikenInputException exception)
        {
            await WriteExceptionAsync("input", exception.Message);
            return 2;
        }
        catch (FikenPolicyException exception)
        {
            await WriteExceptionAsync("policy", exception.Message);
            return 3;
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
