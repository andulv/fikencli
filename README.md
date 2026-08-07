# fikencli

`fikencli` is a small, agent-first .NET CLI for direct Fiken API v2 access. It maps implemented API resources and parameters to predictable commands and compact JSON.

## Requirements and build

- .NET 10 SDK

```bash
dotnet restore fikencli.slnx
dotnet build fikencli.slnx --no-restore
dotnet test tests/fikencli.Tests/fikencli.Tests.csproj --no-restore
```

Run from source with `dotnet run --project src/fikencli -- <arguments>`, or use the built `fikencli` executable.

## Authentication

Commands that call Fiken read `FIKEN_API_TOKEN` from the environment immediately before the request:

```bash
export FIKEN_API_TOKEN='your-personal-token'
fikencli user get
```

Do not pass tokens as CLI arguments, write them to output, or commit them to a file. Previewing a write does not require a token.

## Command map

```text
user get
companies list|get
accounts list|get
account-balances list|get
transactions get
journal-entries list|get
general-journal-entries create
```

Run `fikencli --help`, then `fikencli <group> <command> --help`, before constructing an unfamiliar call.

The mapping is mechanical:

- API resource names become kebab-case command groups: `accountBalances` → `account-balances`.
- API camelCase query parameters become kebab-case options: `pageSize` → `--page-size`.
- Company-scoped calls require `--company-slug`; the CLI never selects a company.
- Dates use `YYYY-MM-DD`, amounts remain integer øre, and account codes remain strings.
- Pagination is explicit. Set `--page` and `--page-size`; the CLI never fetches another page.
- Each invocation sends at most one request. The CLI does not join, aggregate, or interpret accounting data.

Examples:

```bash
fikencli companies list --page 0 --page-size 50 --sort-by 'name asc'
fikencli accounts get --company-slug my-company --account-code '1500:10001'
fikencli account-balances list --company-slug my-company --date 2026-07-31 --page 0 --page-size 100
fikencli journal-entries list --company-slug my-company --date-ge 2026-07-01 --date-lt 2026-08-01
fikencli transactions get --company-slug my-company --transaction-id 12345
```

## Output, errors, and exit codes

Successful API data is compact JSON on stdout without a CLI envelope. Successful empty-body writes return compact JSON with `status` and `location`. Diagnostics are written to stderr.

- `0`: success
- `1`: HTTP or unexpected failure
- `2`: command input or configuration failure
- `130`: cancellation

HTTP failures use a stable JSON error containing the HTTP status, Fiken request ID, and original Fiken response body. The CLI does not replace errors with empty data or fallback values.

## General journal entries: preview first

Provide one Fiken-shaped JSON request through `--input` or `--stdin`. The default is a local preview: it validates JSON readability, prints the exact relative target and parsed payload, and makes no HTTP request.

```bash
fikencli general-journal-entries create \
  --company-slug my-company \
  --input general-journal-entry.json
```

Inspect the preview. Send only when the write is intended:

```bash
fikencli general-journal-entries create \
  --company-slug my-company \
  --input general-journal-entry.json \
  --execute
```

Preview does not validate accounting correctness or the complete Fiken schema. Fiken is authoritative. Never add `--execute` merely to test syntax, and do not execute a write without explicit user authorization after payload review.

## Agent Skill

The self-contained Agent Skill is in [`skills/fikencli/SKILL.md`](skills/fikencli/SKILL.md).
