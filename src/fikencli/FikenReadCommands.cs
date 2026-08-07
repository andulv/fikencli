using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;

namespace FikenCli;

public static class FikenReadCommands
{
    public static void AddTo(RootCommand root, FikenTransport transport, TextWriter output, TextWriter error)
    {
        AddUser(root, transport, output, error);
        AddCompanies(root, transport, output, error);
        AddAccounts(root, transport, output, error);
        AddAccountBalances(root, transport, output, error);
        AddTransactions(root, transport, output, error);
        AddJournalEntries(root, transport, output, error);
    }

    private static void AddUser(RootCommand root, FikenTransport transport, TextWriter output, TextWriter error)
    {
        var group = new Command("user", "Fiken API resource: /user.");
        group.Subcommands.Add(Get("get", "GET /user: return the authenticated user.", _ => "user"));
        root.Subcommands.Add(group);

        Command Get(string name, string description, Func<ParseResult, string> path) =>
            FikenCommandFactory.CreateGetCommand(name, description, path, transport, output, error);
    }

    private static void AddCompanies(RootCommand root, FikenTransport transport, TextWriter output, TextWriter error)
    {
        var group = new Command("companies", "Fiken API resource: /companies.");
        var page = PageOption();
        var pageSize = PageSizeOption();
        var sortBy = new Option<string?>("--sort-by") { Description = "Fiken query parameter sortBy." };
        sortBy.AcceptOnlyFromAmong("createdDate asc", "createdDate desc", "name asc", "name desc", "organizationNumber asc", "organizationNumber desc");
        group.Subcommands.Add(FikenCommandFactory.CreateGetCommand(
            "list", "GET /companies: list accessible companies.",
            result => Query("companies", ("page", result.GetValue(page)), ("pageSize", result.GetValue(pageSize)), ("sortBy", result.GetValue(sortBy))),
            transport, output, error, page, pageSize, sortBy));

        var slug = RequiredString("--company-slug", "Fiken path parameter companySlug.");
        group.Subcommands.Add(FikenCommandFactory.CreateGetCommand(
            "get", "GET /companies/{companySlug}: get one company.",
            result => $"companies/{Escape(result.GetValue(slug)!)}",
            transport, output, error, slug));
        root.Subcommands.Add(group);
    }

    private static void AddAccounts(RootCommand root, FikenTransport transport, TextWriter output, TextWriter error)
    {
        var group = new Command("accounts", "Fiken API resource: /companies/{companySlug}/accounts.");
        var listSlug = RequiredString("--company-slug", "Fiken path parameter companySlug.");
        var from = new Option<long?>("--from-account") { Description = "Fiken query parameter fromAccount (excludes subaccounts)." };
        var to = new Option<long?>("--to-account") { Description = "Fiken query parameter toAccount (excludes subaccounts)." };
        var page = PageOption();
        var pageSize = PageSizeOption();
        group.Subcommands.Add(FikenCommandFactory.CreateGetCommand(
            "list", "GET /companies/{companySlug}/accounts: list bookkeeping accounts.",
            result => Query($"companies/{Escape(result.GetValue(listSlug)!)}/accounts", ("fromAccount", result.GetValue(from)), ("toAccount", result.GetValue(to)), ("page", result.GetValue(page)), ("pageSize", result.GetValue(pageSize))),
            transport, output, error, listSlug, from, to, page, pageSize));

        var getSlug = RequiredString("--company-slug", "Fiken path parameter companySlug.");
        var accountCode = RequiredString("--account-code", "Fiken path parameter accountCode; for example 3020 or 1500:10001.");
        group.Subcommands.Add(FikenCommandFactory.CreateGetCommand(
            "get", "GET /companies/{companySlug}/accounts/{accountCode}: get one bookkeeping account.",
            result => $"companies/{Escape(result.GetValue(getSlug)!)}/accounts/{Escape(result.GetValue(accountCode)!)}",
            transport, output, error, getSlug, accountCode));
        root.Subcommands.Add(group);
    }

    private static void AddAccountBalances(RootCommand root, FikenTransport transport, TextWriter output, TextWriter error)
    {
        var group = new Command("account-balances", "Fiken API resource: /companies/{companySlug}/accountBalances.");
        var listSlug = RequiredString("--company-slug", "Fiken path parameter companySlug.");
        var listDate = RequiredDate("--date", "Required Fiken query parameter date (YYYY-MM-DD).");
        var from = new Option<long?>("--from-account") { Description = "Fiken query parameter fromAccount (excludes subaccounts)." };
        var to = new Option<long?>("--to-account") { Description = "Fiken query parameter toAccount (excludes subaccounts)." };
        var page = PageOption();
        var pageSize = PageSizeOption();
        group.Subcommands.Add(FikenCommandFactory.CreateGetCommand(
            "list", "GET /companies/{companySlug}/accountBalances: list balances for a required date.",
            result => Query($"companies/{Escape(result.GetValue(listSlug)!)}/accountBalances", ("date", Date(result.GetValue(listDate))), ("fromAccount", result.GetValue(from)), ("toAccount", result.GetValue(to)), ("page", result.GetValue(page)), ("pageSize", result.GetValue(pageSize))),
            transport, output, error, listSlug, listDate, from, to, page, pageSize));

        var getSlug = RequiredString("--company-slug", "Fiken path parameter companySlug.");
        var getDate = RequiredDate("--date", "Required Fiken query parameter date (YYYY-MM-DD).");
        var accountCode = RequiredString("--account-code", "Fiken path parameter accountCode; for example 3020 or 1500:10001.");
        group.Subcommands.Add(FikenCommandFactory.CreateGetCommand(
            "get", "GET /companies/{companySlug}/accountBalances/{accountCode}: get one balance for a required date.",
            result => Query($"companies/{Escape(result.GetValue(getSlug)!)}/accountBalances/{Escape(result.GetValue(accountCode)!)}", ("date", Date(result.GetValue(getDate)))),
            transport, output, error, getSlug, accountCode, getDate));
        root.Subcommands.Add(group);
    }

    private static void AddTransactions(RootCommand root, FikenTransport transport, TextWriter output, TextWriter error)
    {
        var group = new Command("transactions", "Fiken API resource: /companies/{companySlug}/transactions/{transactionId}.");
        var slug = RequiredString("--company-slug", "Fiken path parameter companySlug.");
        var id = RequiredPositiveLong("--transaction-id", "Fiken path parameter transactionId.");
        group.Subcommands.Add(FikenCommandFactory.CreateGetCommand(
            "get", "GET /companies/{companySlug}/transactions/{transactionId}: get one transaction.",
            result => $"companies/{Escape(result.GetValue(slug)!)}/transactions/{result.GetValue(id)}",
            transport, output, error, slug, id));
        root.Subcommands.Add(group);
    }

    private static void AddJournalEntries(RootCommand root, FikenTransport transport, TextWriter output, TextWriter error)
    {
        var group = new Command("journal-entries", "Fiken API resource: /companies/{companySlug}/journalEntries.");
        var listSlug = RequiredString("--company-slug", "Fiken path parameter companySlug.");
        var page = PageOption();
        var pageSize = PageSizeOption();
        var date = OptionalDate("--date", "Fiken query parameter date (YYYY-MM-DD).");
        var dateLe = OptionalDate("--date-le", "Fiken query parameter dateLe (YYYY-MM-DD).");
        var dateLt = OptionalDate("--date-lt", "Fiken query parameter dateLt (YYYY-MM-DD).");
        var dateGe = OptionalDate("--date-ge", "Fiken query parameter dateGe (YYYY-MM-DD).");
        var dateGt = OptionalDate("--date-gt", "Fiken query parameter dateGt (YYYY-MM-DD).");
        group.Subcommands.Add(FikenCommandFactory.CreateGetCommand(
            "list", "GET /companies/{companySlug}/journalEntries: list posted journal entries.",
            result => Query($"companies/{Escape(result.GetValue(listSlug)!)}/journalEntries", ("page", result.GetValue(page)), ("pageSize", result.GetValue(pageSize)), ("date", Date(result.GetValue(date))), ("dateLe", Date(result.GetValue(dateLe))), ("dateLt", Date(result.GetValue(dateLt))), ("dateGe", Date(result.GetValue(dateGe))), ("dateGt", Date(result.GetValue(dateGt)))),
            transport, output, error, listSlug, page, pageSize, date, dateLe, dateLt, dateGe, dateGt));

        var getSlug = RequiredString("--company-slug", "Fiken path parameter companySlug.");
        var id = RequiredPositiveLong("--journal-entry-id", "Fiken path parameter journalEntryId.");
        group.Subcommands.Add(FikenCommandFactory.CreateGetCommand(
            "get", "GET /companies/{companySlug}/journalEntries/{journalEntryId}: get one posted journal entry.",
            result => $"companies/{Escape(result.GetValue(getSlug)!)}/journalEntries/{result.GetValue(id)}",
            transport, output, error, getSlug, id));
        root.Subcommands.Add(group);
    }

    private static Option<string> RequiredString(string name, string description)
    {
        var option = new Option<string>(name) { Description = description, Required = true };
        option.Validators.Add(result =>
        {
            if (string.IsNullOrWhiteSpace(result.GetValueOrDefault<string>())) result.AddError($"{name} must not be blank.");
        });
        return option;
    }

    private static Option<long> RequiredPositiveLong(string name, string description)
    {
        var option = new Option<long>(name) { Description = description, Required = true };
        option.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<long>() <= 0) result.AddError($"{name} must be greater than zero.");
        });
        return option;
    }

    private static Option<int?> PageOption()
    {
        var option = new Option<int?>("--page") { Description = "Fiken query parameter page (minimum 0; use with pageSize)." };
        option.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<int?>() is < 0) result.AddError("--page must be at least 0.");
        });
        return option;
    }

    private static Option<int?> PageSizeOption()
    {
        var option = new Option<int?>("--page-size") { Description = "Fiken query parameter pageSize (1-100; use with page)." };
        option.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<int?>() is int value && (value < 1 || value > 100)) result.AddError("--page-size must be between 1 and 100.");
        });
        return option;
    }

    private static Option<DateOnly> RequiredDate(string name, string description)
    {
        var option = new Option<DateOnly>(name) { Description = description, Required = true };
        option.CustomParser = result => ParseDate(result, name) ?? default;
        return option;
    }

    private static Option<DateOnly?> OptionalDate(string name, string description)
    {
        var option = new Option<DateOnly?>(name) { Description = description };
        option.CustomParser = result => ParseDate(result, name);
        return option;
    }

    private static DateOnly? ParseDate(ArgumentResult result, string name)
    {
        var text = result.Tokens.SingleOrDefault()?.Value;
        if (text is not null && DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)) return value;
        result.AddError($"{name} must use YYYY-MM-DD.");
        return null;
    }

    private static string? Date(DateOnly? value) => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static string Escape(string value) => Uri.EscapeDataString(value);

    private static string Query(string path, params (string Name, object? Value)[] values)
    {
        var supplied = values.Where(pair => pair.Value is not null).Select(pair => $"{pair.Name}={Uri.EscapeDataString(Convert.ToString(pair.Value, CultureInfo.InvariantCulture)!)}");
        var query = string.Join("&", supplied);
        return query.Length == 0 ? path : $"{path}?{query}";
    }
}
