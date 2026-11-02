# AURA API

ASP.NET Core 8 minimal API with ASP.NET Core Identity, JWT authentication, Entity Framework Core, and PostgreSQL for non-Development environments.

## Local development

From PowerShell:

```powershell
Set-Location .\backend\src\Aura.Api
$localSdk = Join-Path $env:USERPROFILE ".dotnet"
if (Test-Path (Join-Path $localSdk "dotnet.exe")) {
  $env:DOTNET_ROOT = $localSdk
  $env:PATH = "$localSdk;$env:PATH"
}
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --urls http://localhost:5000
```

Development uses SQLite in `aura-dev.db`; other environments use the configured PostgreSQL connection string. The API creates the local database schema on startup.

Set an Anthropic key when you want to use AI generation:

```powershell
dotnet user-secrets init
dotnet user-secrets set "Aura:Ai:ApiKey" "YOUR_ANTHROPIC_API_KEY"
```

Build from PowerShell with the local .NET SDK on `PATH`:

```powershell
$localSdk = Join-Path $env:USERPROFILE ".dotnet"
if (Test-Path (Join-Path $localSdk "dotnet.exe")) {
  $env:DOTNET_ROOT = $localSdk
  $env:PATH = "$localSdk;$env:PATH"
}
Set-Location .\src\Aura.Api
dotnet build
```

Configuration options are in `src/Aura.Api/appsettings.json`; local overrides are in `appsettings.Development.json`.

## Main source areas

- `src/Aura.Api/Program.cs`: application setup, endpoints, authentication, and database initialization.
- `src/Aura.Api/Models/`: EF Core entities and configuration options.
- `src/Aura.Api/Services/`: AI/email services, payment endpoints, and plan maintenance.
- `src/Aura.Api/appsettings*.json`: application configuration by environment.

See [API endpoint notes](../docs/API/README.md) and [deployment instructions](../docs/setup/deployment.md).
