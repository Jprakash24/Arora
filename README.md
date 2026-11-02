# AURA

AURA is an AI workspace with a React frontend and an ASP.NET Core Web API. The frontend provides the public site, AI tools, authentication, user dashboard, and admin screens. The API handles accounts, credits, AI requests, history, and optional Razorpay payments.

## Project layout

```text
AURA/
├── backend/
│   ├── src/Aura.Api/
│   │   ├── Models/        Data models and configuration options
│   │   ├── Services/      AI, email, billing, and plan maintenance
│   │   ├── Program.cs     API setup, routes, authentication, and startup
│   │   ├── appsettings*.json
│   │   └── Aura.Api.csproj
│   ├── Dockerfile
│   └── README.md
├── frontend/
│   ├── src/
│   │   ├── components/    Reusable visual components
│   │   ├── context/       React authentication state
│   │   ├── pages/         Home, tools, pricing, and account screens
│   │   ├── routes/        Application routes and shared page shell
│   │   ├── services/      API client
│   │   ├── styles/        Global styles
│   │   └── main.tsx       Frontend entry point
│   ├── package.json
│   └── README.md
├── docs/
│   ├── API/
│   ├── architecture/
│   └── setup/
├── docker-compose.yml     Optional PostgreSQL for non-SQLite development
└── .gitignore
```

The source is grouped by the technologies actually used (.NET and React); there is no Node.js backend. Folders for assets, tests, or scripts are not included until the project has files that need them.

## Requirements

- .NET 8 SDK
- Node.js and npm
- Docker Desktop only if you want to run PostgreSQL locally

## Run locally on Windows

Start the API in PowerShell terminal 1:

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

The Development configuration uses a local SQLite database at `backend/src/Aura.Api/aura-dev.db` and creates its tables on first start. Docker and PostgreSQL are not required for this setup.

Start the frontend in PowerShell terminal 2:

```powershell
Set-Location .\frontend
npm install
npm run dev
```

Open the Vite URL printed in the terminal (normally `http://localhost:5173`). The API health check is at `http://localhost:5000/health`.

AI generation needs an Anthropic API key. Configure it for local development without putting it in source control:

```powershell
Set-Location .\backend\src\Aura.Api
dotnet user-secrets init
dotnet user-secrets set "Aura:Ai:ApiKey" "YOUR_ANTHROPIC_API_KEY"
```

Restart the API after setting the key. Development mode disables email confirmation because the configured email sender only writes messages to the API log. A local admin is seeded as `admin@aura.local` with password `ChangeMe-Admin-123`; this is for local development only and must not be used in production.

## Build and verification

```powershell
Set-Location .\backend\src\Aura.Api
$localSdk = Join-Path $env:USERPROFILE ".dotnet"
if (Test-Path (Join-Path $localSdk "dotnet.exe")) {
  $env:DOTNET_ROOT = $localSdk
  $env:PATH = "$localSdk;$env:PATH"
}
dotnet build

Set-Location ..\..\..\frontend
npm run build
```

There are currently no automated test projects. See [docs/API/README.md](./docs/API/README.md), [docs/architecture/overview.md](./docs/architecture/overview.md), and [docs/setup/deployment.md](./docs/setup/deployment.md) for additional information.
