using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Aura.Api;

public static class Billing
{
    public static void MapBilling(this WebApplication app, AuraOptions cfg)
    {
        // Creates a Razorpay order on the server. The price always comes from config, never from the browser.
        app.MapPost("/api/billing/checkout", async (CheckoutReq r, ClaimsPrincipal p, AppDbContext db,
            IHttpClientFactory hf, ILogger<Payment> log, CancellationToken ct) =>
        {
            if (!cfg.Plans.TryGetValue(r.Plan ?? "", out var plan) || plan.PriceInr <= 0)
                return Results.BadRequest(new { error = "Choose a paid plan." });
            if (string.IsNullOrEmpty(cfg.Razorpay.KeyId) || string.IsNullOrEmpty(cfg.Razorpay.KeySecret))
                return Results.Json(new { error = "Payments are not configured yet." }, statusCode: 503);

            var uid = p.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var paise = (long)(plan.PriceInr * 100);
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.razorpay.com/v1/orders");
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{cfg.Razorpay.KeyId}:{cfg.Razorpay.KeySecret}")));
            req.Content = JsonContent.Create(new
            {
                amount = paise, currency = "INR",
                receipt = "aura_" + Guid.NewGuid().ToString("N")[..16],
                notes = new { userId = uid, plan = r.Plan }
            });
            using var res = await hf.CreateClient().SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                log.LogError("Razorpay order failed {Status}: {Body}", (int)res.StatusCode, body);
                return Results.Json(new { error = "Could not start the payment. Try again." }, statusCode: 502);
            }
            var orderId = JsonDocument.Parse(body).RootElement.GetProperty("id").GetString()!;
            db.Payments.Add(new Payment { OrderId = orderId, UserId = uid, Plan = r.Plan!, AmountPaise = paise });
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { orderId, keyId = cfg.Razorpay.KeyId, amount = paise, currency = "INR" });
        }).RequireAuthorization().RequireRateLimiting("auth");

        // The ONLY place credits and plans are granted. Verified by Razorpay's HMAC signature.
        app.MapPost("/api/billing/webhook", async (HttpRequest rq, AppDbContext db, ILogger<Payment> log, CancellationToken ct) =>
        {
            var secret = cfg.Razorpay.WebhookSecret;
            if (string.IsNullOrEmpty(secret)) return Results.StatusCode(503);
            using var sr = new StreamReader(rq.Body);
            var raw = await sr.ReadToEndAsync(ct);
            var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
            var sig = rq.Headers["X-Razorpay-Signature"].ToString();
            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(sig)))
                return Results.Unauthorized();

            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.GetProperty("event").GetString() != "payment.captured") return Results.Ok();
            var pe = doc.RootElement.GetProperty("payload").GetProperty("payment").GetProperty("entity");
            var orderId = pe.GetProperty("order_id").GetString();
            var payId = pe.GetProperty("id").GetString();
            var amount = pe.GetProperty("amount").GetInt64();

            var pay = await db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.OrderId == orderId, ct);
            if (pay is null || pay.AmountPaise != amount || !cfg.Plans.TryGetValue(pay.Plan, out var plan))
            {
                log.LogWarning("Webhook ignored: unknown order or amount mismatch ({Order})", orderId);
                return Results.Ok();
            }

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var now = DateTime.UtcNow;
            var planName = pay.Plan;
            var credits = plan.MonthlyCredits;
            // Status flips from Created to Paid exactly once, so repeated webhooks can never double-credit.
            var claimed = await db.Payments.Where(x => x.Id == pay.Id && x.Status == "Created")
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Paid").SetProperty(x => x.PaymentId, payId)
                    .SetProperty(x => x.PaidAt, (DateTime?)now), ct);
            if (claimed == 0) return Results.Ok();
            await db.Users.Where(u => u.Id == pay.UserId).ExecuteUpdateAsync(s => s
                .SetProperty(u => u.Plan, planName)
                .SetProperty(u => u.Credits, u => u.Credits + credits)
                .SetProperty(u => u.PlanExpiresAt, (DateTime?)now.AddDays(30)), ct);
            await tx.CommitAsync(ct);
            log.LogInformation("Plan {Plan} activated for user {User}", planName, pay.UserId);
            return Results.Ok();
        });
    }
}

// Hourly: expire paid plans after 30 days and refill the free monthly credits.
public class PlanMaintenance(IServiceScopeFactory sf, AuraOptions cfg, ILogger<PlanMaintenance> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = sf.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var now = DateTime.UtcNow;
                var cutoff = now.AddDays(-30);
                var free = cfg.Plans.GetValueOrDefault("Free")?.MonthlyCredits ?? 0;
                await db.Users.Where(u => u.Plan != "Free" && u.PlanExpiresAt != null && u.PlanExpiresAt < now)
                    .ExecuteUpdateAsync(s => s.SetProperty(u => u.Plan, "Free").SetProperty(u => u.PlanExpiresAt, (DateTime?)null), ct);
                await db.Users.Where(u => u.Plan == "Free" && u.LastRefillAt < cutoff)
                    .ExecuteUpdateAsync(s => s.SetProperty(u => u.Credits, u => u.Credits < free ? free : u.Credits)
                        .SetProperty(u => u.LastRefillAt, now), ct);
            }
            catch (Exception e) when (e is not OperationCanceledException) { log.LogError(e, "Plan maintenance failed"); }
            try { await Task.Delay(TimeSpan.FromHours(1), ct); } catch (OperationCanceledException) { }
        }
    }
}
