# AURA frontend

React 18, TypeScript, Vite, Tailwind CSS, and React Router frontend.

## Local development

From PowerShell:

```powershell
Set-Location .\frontend
npm install
npm run dev
```

The frontend expects the API at `http://localhost:5000` by default. To use a different API URL, copy `.env.example` to `.env` and set `VITE_API_URL`. Vite loads `.env` on startup, so restart the dev server after changing it.

## Build

```powershell
npm run build
```

This runs the TypeScript check before building the production bundle into `dist/`.
