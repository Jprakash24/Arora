# Deploy AURA (free preview)

The checked-in `render.yaml` describes the API service for Render. The frontend is configured for Netlify in `frontend/netlify.toml`. This preview setup uses a Neon PostgreSQL database and Render's free web service; the API may sleep when idle and take time to wake up.

## 1. Push the source to GitHub

The repository root is the `AURA` directory. Do not push `node_modules`, `dist`, local databases, `.env` files, or `appsettings.Development.json`. The root `.gitignore` excludes these. The frontend `.env.example` contains only the local development URL and is safe to commit.

## 2. Create the production database

Create a PostgreSQL database in Neon and copy its connection string. Keep it private; do not put it in a source file or share it in chat.

For an optional local PostgreSQL container, copy the root `.env.example` to `.env`, replace the placeholder with a local-only password, then run `docker compose up -d` from the repository root. The `.env` file is ignored by Git.

## 3. Deploy the API to Render

In Render, create a Blueprint from the GitHub repository and use the `render.yaml` at the repository root. Set the prompted values in Render's environment settings:

- `ConnectionStrings__Default`: the Neon connection string.
- `Aura__Ai__ApiKey`: an Anthropic API key. Without it, AI generation will not work.
- `Aura__FrontendUrl`: the Netlify site URL, e.g. `https://your-site.netlify.app`.
- `Aura__CorsOrigins__0`: the same origin only, e.g. `https://your-site.netlify.app` (no trailing slash).
- `Aura__AdminEmail` and `Aura__AdminPassword`: a new admin login; use a unique strong password.

Render generates `Aura__Jwt__Key` for the service. Email confirmation is disabled for this preview because the current email sender only writes links to application logs. Password reset and email confirmation messages are therefore not delivered to users.

After deployment, verify `https://YOUR-API.onrender.com/health` returns `{"status":"ok"}`.

## 4. Deploy the frontend to Netlify

Import the same GitHub repository into Netlify. Set the base directory to `frontend`; Netlify will use `frontend/netlify.toml` to build with `npm run build` and publish `dist`.

Add the frontend build environment variable `VITE_API_URL` with the deployed API URL, e.g. `https://YOUR-API.onrender.com`. Trigger a new deploy after setting it. The API's `Aura__FrontendUrl` and `Aura__CorsOrigins__0` must match the final Netlify origin exactly.

## Optional payments and production readiness

Checkout stays unavailable until Razorpay credentials are configured. Set `Aura__Razorpay__KeyId`, `Aura__Razorpay__KeySecret`, and `Aura__Razorpay__WebhookSecret` in Render, then configure Razorpay's `payment.captured` webhook at `https://YOUR-API.onrender.com/api/billing/webhook`. Test payments before enabling live credentials.

Before accepting real customers, configure a real email provider, review the privacy policy and terms, use production secrets, and establish a database migration/backup strategy. The current startup schema creation is suitable for a fresh preview database, not as a substitute for production migrations.
