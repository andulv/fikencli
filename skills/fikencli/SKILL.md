---
name: fikencli
description: "Uses the fikencli command-line client for direct Fiken API v2 reads and preview-first general journal entry creation. Use when a user asks to inspect Fiken companies, accounts, dated balances, transactions, or journal entries, or explicitly requests a Fiken fri postering write."
metadata:
  version: "1.0"
---
# fikencli

Use `fikencli` for compact, one-request access to the implemented Fiken API v2 resources.

## Safety

- Never put `FIKEN_API_TOKEN` in command arguments, output, chat, or committed files. API commands read it from the environment.
- Read operations are safe to run when the requested company and parameters are clear.
- `general-journal-entries create` previews by default. **Never add `--execute` unless the user explicitly requested the write after the payload and target were inspected.**
- Preview checks JSON readability and targeting only. Do not claim that it validates Fiken's schema, balancing, VAT treatment, or accounting correctness.
- Do not guess a company slug, account code, date, resource ID, or accounting treatment. Ask the user when a required value is missing.

## Procedure

1. Run `fikencli --help`, then `fikencli <group> <command> --help`. Do not guess syntax.
2. Select an implemented direct command:
   - `user get`
   - `companies list|get`
   - `accounts list|get`
   - `account-balances list|get`
   - `transactions get`
   - `journal-entries list|get`
   - `general-journal-entries create`
3. Translate API names mechanically:
   - resource `accountBalances` → group `account-balances`;
   - query `pageSize` → option `--page-size`;
   - every company-scoped command needs `--company-slug`;
   - dates are `YYYY-MM-DD`, amounts are integer øre, and account codes are strings.
4. Supply pagination explicitly when needed. One invocation sends at most one request; it does not auto-page, join, aggregate, or calculate reports.
5. Parse compact JSON from stdout. On non-zero exit, inspect structured diagnostics on stderr. Never replace a failure with empty data.

## Read examples

```bash
fikencli companies list --page 0 --page-size 50 --sort-by 'name asc'
fikencli accounts get --company-slug my-company --account-code '1500:10001'
fikencli account-balances list --company-slug my-company --date 2026-07-31 --page 0 --page-size 100
fikencli journal-entries list --company-slug my-company --date-ge 2026-07-01 --date-lt 2026-08-01
fikencli transactions get --company-slug my-company --transaction-id 12345
```

Do not infer unsupported list commands. In particular, this version exposes direct transaction lookup by ID, not a transaction-list endpoint.

## General journal entry workflow

Prepare one Fiken-shaped JSON object. Preserve field names, signed integer øre amounts, account-code strings, and VAT codes without reinterpretation.

Preview from a file:

```bash
fikencli general-journal-entries create \
  --company-slug my-company \
  --input general-journal-entry.json
```

Or preview from stdin:

```bash
printf '%s' "$PAYLOAD" | fikencli general-journal-entries create \
  --company-slug my-company \
  --stdin
```

Inspect the preview's `method`, `path`, and `payload`. If and only if the user authorized that write, repeat the same command with `--execute`. A successful empty API response returns `status` and `location` JSON.

## Failure handling

- Missing/blank token or invalid CLI input: correct the input; do not retry with guessed values.
- HTTP failure: report its status, request ID, and Fiken body from stderr.
- `401`/`403`: ask the user to check token access; never request that they paste the token into chat.
- `404`: verify the company slug and resource ID.
- Fiken/schema rejection: show the error and ask for correction. Do not silently alter accounting payloads.
