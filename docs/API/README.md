# API endpoint overview

Local base URL: `http://localhost:5000`

## Public

- `GET /health`
- `GET /api/tools`
- `GET /api/plans`
- `POST /api/auth/signup`
- `POST /api/auth/login`
- `POST /api/auth/confirm-email`
- `POST /api/auth/forgot-password`
- `POST /api/auth/reset-password`
- `POST /api/billing/webhook` (Razorpay-signed webhook)

## Authenticated user

Send `Authorization: Bearer <token>` for protected endpoints.

- `GET /api/me`
- `GET /api/history`
- `GET /api/dashboard`
- `POST /api/ai/generate`
- `POST /api/billing/checkout`

## Admin

Requires the Admin role in the JWT:

- `GET /api/admin/stats`
- `GET /api/admin/users`
- `POST /api/admin/users/{id}/credits`
