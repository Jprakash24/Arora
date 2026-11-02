# Put AURA online (free preview)

This guide deploys the frontend to Netlify, the API to Render, and the database to Neon. A free Render service may sleep when idle, so the first API request after a quiet period can take a little while. Free-tier availability and limits depend on each provider.

## Before you start

- The source is in the public GitHub repository: `https://github.com/Jprakash24/Arora`.
- Create accounts with GitHub, Netlify, Render, Neon, and Anthropic.
- Never put provider credentials in GitHub, project files, screenshots, or chat. Enter secrets only in the provider's private environment-variable settings.
- The project root is the directory containing `render.yaml`, `backend`, and `frontend`.

## Step 1: Create the Neon database

1. Sign in to Neon and create a PostgreSQL project.
2. Open the database connection details and copy the connection string for the application.
3. Keep it private. You will paste it into Render as `ConnectionStrings__Default`; do not commit it to the repository.

## Step 2: Deploy the frontend to Netlify first

Deploy the frontend first so you know its public URL before configuring API CORS.

1. In Netlify, choose **Add new site** / **Import an existing project**, connect GitHub, and select `Jprakash24/Arora`.
2. Configure the build:
   - Base directory: `frontend`
   - Build command: `npm run build`
   - Publish directory: `dist`
3. Deploy the site. The initial deployment is temporary until the API URL is configured.
4. Copy the site URL Netlify assigns, such as `https://your-site-name.netlify.app`. Use the origin only: no path and no trailing slash.

The repository's `frontend/netlify.toml` already contains the build and single-page-app redirect settings.

## Step 3: Create the Render API service

1. In Render, choose **New** → **Blueprint** and connect the same GitHub repository.
2. Select the `main` branch and use the repository's root `render.yaml`.
3. Render reads the Blueprint and creates an `aura-api` Docker web service. When asked for values, enter:

   | Render variable | Value |
   | --- | --- |
   | `ConnectionStrings__Default` | Neon connection string from Step 1 |
   | `Aura__Ai__ApiKey` | Anthropic API key |
   | `Aura__FrontendUrl` | Netlify origin from Step 2, e.g. `https://your-site-name.netlify.app` |
   | `Aura__CorsOrigins__0` | The same Netlify origin, exactly; no trailing slash |
   | `Aura__AdminEmail` | Email address for your initial admin account |
   | `Aura__AdminPassword` | A new, unique, strong password for that account |

4. The Blueprint generates `Aura__Jwt__Key` for you. Keep the generated value private and do not replace it with a short or reused password.
5. If Render does not prompt for the `sync: false` variables, open the service's **Environment** settings, add them there, save, and redeploy.
6. Wait for the service deployment to finish. Copy the Render service URL, such as `https://aura-api.onrender.com`.
7. Check `https://YOUR-API.onrender.com/health`. It should return `{"status":"ok"}`.

For this preview, email confirmation is disabled. The current email sender only writes confirmation and password-reset links to server logs; it does not deliver email.

## Step 4: Connect Netlify to the API

1. In Netlify, open the site's **Site configuration** → **Environment variables**.
2. Add `VITE_API_URL` with the Render service URL, e.g. `https://aura-api.onrender.com` (no trailing slash).
3. Trigger **Clear cache and deploy site** / a new deploy so Vite bakes the URL into the frontend build.
4. Check the Render environment values `Aura__FrontendUrl` and `Aura__CorsOrigins__0` still match the deployed Netlify origin exactly. If you change the Netlify domain later, update both Render variables and redeploy the API.

## Step 5: Verify the live site

1. Open the Netlify URL and refresh the page.
2. Visit `/tools`; the tools list should load from the API.
3. Create an account and log in. With a valid Anthropic key, open a tool and submit a small test prompt.
4. Check the dashboard and generation history after a successful result.
5. If requests fail, check the browser developer console, Netlify deploy log, and Render service logs. Confirm the API URL has no trailing slash and the Render CORS origin exactly matches the Netlify origin.

## Preview limitations and optional services

- Free Render services can sleep when idle. Startup requests may be slow; this does not necessarily mean the service is down.
- AI generation requires a valid Anthropic API key and may incur provider charges.
- Checkout is unavailable until Razorpay is configured. To enable it, add `Aura__Razorpay__KeyId`, `Aura__Razorpay__KeySecret`, and `Aura__Razorpay__WebhookSecret` in Render, then configure Razorpay's `payment.captured` webhook at `https://YOUR-API.onrender.com/api/billing/webhook`. Test in Razorpay Test Mode first.
- Email confirmation and password reset are not customer-ready until a real email provider is implemented and configured.
- Before accepting real customers, review the privacy policy and terms, arrange backups, and replace startup schema creation with a tested database migration strategy.
