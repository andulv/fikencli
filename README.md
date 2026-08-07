# fikencli

A small, agent-first command-line client for the Fiken API v2.

## Requirements

- .NET 10 SDK

## Build

```bash
dotnet restore fikencli.slnx
dotnet build fikencli.slnx --no-restore
dotnet test tests/fikencli.Tests/fikencli.Tests.csproj --no-restore
```

The Fiken command surface will be added incrementally. Do not store API tokens in this repository.
