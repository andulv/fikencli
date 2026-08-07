using System.CommandLine;
using System.Text.Json;

namespace FikenCli;

public static class FikenWriteCommands
{
    public static void AddTo(
        RootCommand root,
        FikenTransport transport,
        TextReader input,
        TextWriter output,
        TextWriter error)
    {
        var group = new Command("general-journal-entries", "Fiken API resource: /companies/{companySlug}/generalJournalEntries.");
        var create = new Command("create", "POST /companies/{companySlug}/generalJournalEntries. Previews by default; sends only with --execute.");
        var companySlug = new Option<string>("--company-slug")
        {
            Description = "Required Fiken path parameter companySlug.",
            Required = true
        };
        companySlug.Validators.Add(result =>
        {
            if (string.IsNullOrWhiteSpace(result.GetValueOrDefault<string>())) result.AddError("--company-slug must not be blank.");
        });
        var inputFile = new Option<FileInfo?>("--input")
        {
            Description = "Read one Fiken generalJournalEntryRequest JSON object from this file. Mutually exclusive with --stdin."
        };
        var useStdin = new Option<bool>("--stdin")
        {
            Description = "Read one Fiken generalJournalEntryRequest JSON object from stdin. Mutually exclusive with --input."
        };
        var execute = new Option<bool>("--execute")
        {
            Description = "Send the POST. Without this option, output a preview and make no request."
        };
        create.Options.Add(companySlug);
        create.Options.Add(inputFile);
        create.Options.Add(useStdin);
        create.Options.Add(execute);
        create.SetAction(async (parseResult, cancellationToken) =>
        {
            var file = parseResult.GetValue(inputFile);
            var stdin = parseResult.GetValue(useStdin);
            if ((file is null) == !stdin)
            {
                await error.WriteLineAsync(JsonSerializer.Serialize(new
                {
                    error = new { type = "input", message = "Specify exactly one input source: --input <file> or --stdin." }
                }));
                return 2;
            }

            var path = $"companies/{Uri.EscapeDataString(parseResult.GetValue(companySlug)!)}/generalJournalEntries";
            transport.Authorize(HttpMethod.Post, path);
            var raw = file is not null
                ? await File.ReadAllTextAsync(file.FullName, cancellationToken)
                : await input.ReadToEndAsync(cancellationToken);
            var payload = ParsePayload(raw);
            var compactPayload = JsonSerializer.Serialize(payload);

            if (!parseResult.GetValue(execute))
            {
                await output.WriteLineAsync(JsonSerializer.Serialize(new
                {
                    method = "POST",
                    path,
                    payload
                }));
                return 0;
            }

            return await transport.SendAsync(
                HttpMethod.Post,
                path,
                compactPayload,
                output,
                error,
                cancellationToken);
        });
        group.Subcommands.Add(create);
        root.Subcommands.Add(group);
    }

    private static JsonElement ParsePayload(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new FikenInputException("The JSON input is empty.");
        }

        try
        {
            using var document = JsonDocument.Parse(raw, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new FikenInputException("The JSON input must be one generalJournalEntryRequest object.");
            }
            return document.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            throw new FikenInputException($"The JSON input is malformed: {exception.Message}");
        }
    }
}

public sealed class FikenInputException(string message) : Exception(message);
