using System.CommandLine;

namespace FikenCli;

public static class FikenCommandFactory
{
    public static Command CreateGetCommand(
        string name,
        string description,
        Func<ParseResult, string> relativePath,
        FikenTransport transport,
        TextWriter output,
        TextWriter error,
        params Option[] options)
    {
        var command = new Command(name, description);
        foreach (var option in options)
        {
            command.Options.Add(option);
        }

        command.SetAction((parseResult, cancellationToken) => transport.SendAsync(
            HttpMethod.Get,
            relativePath(parseResult),
            jsonBody: null,
            output,
            error,
            cancellationToken));
        return command;
    }
}
