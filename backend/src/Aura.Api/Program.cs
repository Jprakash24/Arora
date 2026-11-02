using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Aura.Api;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration.GetSection("Aura").Get<AuraOptions>() ?? new AuraOptions();
if (cfg.Jwt.Key.Length < 32)
    throw new InvalidOperationException("Aura:Jwt:Key must be at least 32 characters. Set it with user-secrets or an environment variable.");

builder.Services.AddSingleton(cfg);
var connectionString = builder.Configuration.GetConnectionString("Default");
var useSqlite = builder.Environment.IsDevelopment()
    && string.Equals(builder.Configuration["Database:Provider"], "Sqlite", StringComparison.OrdinalIgnoreCase);
builder.Services.AddDbContext<AppDbContext>(o =>
{
    if (useSqlite) o.UseSqlite(connectionString);
    else o.UseNpgsql(connectionString);
});
builder.Services.AddIdentityCore<AppUser>(o =>
{
    o.Password.RequiredLength = 8;
    o.User.RequireUniqueEmail = true;
    o.Lockout.MaxFailedAccessAttempts = 5;
    o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
}).AddRoles<IdentityRole>().AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = cfg.Jwt.Issuer,
        ValidateAudience = true, ValidAudience = cfg.Jwt.Audience,
        ValidateLifetime = true, ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg.Jwt.Key)),
        ClockSkew = TimeSpan.FromMinutes(1)
    });
builder.Services.AddAuthorization();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(cfg.CorsOrigins).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy("ai", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
});
builder.Services.AddProblemDetails();
builder.Services.AddHostedService<PlanMaintenance>();
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear(); o.KnownProxies.Clear();
});
builder.Services.AddSingleton<IAppEmailSender, LogEmailSender>();
builder.Services.AddHttpClient<AiService>(c => c.Timeout = TimeSpan.FromSeconds(90));

var app = builder.Build();
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Stage 1 creates tables directly. Stage 2 switches to EF Core migrations.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var um = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    foreach (var r in new[] { "Admin", "User" })
        if (!await roles.RoleExistsAsync(r)) await roles.CreateAsync(new IdentityRole(r));
    if (Helpers.IsEmail(cfg.AdminEmail) && cfg.AdminPassword.Length >= 8 && await um.FindByEmailAsync(cfg.AdminEmail) is null)
    {
        var seededAdmin = new AppUser { UserName = cfg.AdminEmail, Email = cfg.AdminEmail, EmailConfirmed = true, DisplayName = "Admin", Plan = "Business", Credits = 100000 };
        if ((await um.CreateAsync(seededAdmin, cfg.AdminPassword)).Succeeded) await um.AddToRoleAsync(seededAdmin, "Admin");
    }
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// ---------- Auth ----------
var auth = app.MapGroup("/api/auth").RequireRateLimiting("auth");

auth.MapPost("/signup", async (SignupReq r, UserManager<AppUser> um, IAppEmailSender mail) =>
{
    if (!Helpers.IsEmail(r.Email)) return Results.BadRequest(new { error = "Enter a valid email address." });
    var email = r.Email.Trim();
    var u = new AppUser
    {
        UserName = email, Email = email, DisplayName = r.Name?.Trim(),
        Credits = cfg.Plans.GetValueOrDefault("Free")?.MonthlyCredits ?? 0,
        EmailConfirmed = !cfg.RequireEmailConfirmation
    };
    var res = await um.CreateAsync(u, r.Password ?? "");
    if (!res.Succeeded) return Results.BadRequest(new { error = string.Join(" ", res.Errors.Select(e => e.Description)) });
    await um.AddToRoleAsync(u, "User");
    if (cfg.RequireEmailConfirmation)
    {
        var token = await um.GenerateEmailConfirmationTokenAsync(u);
        await mail.SendAsync(email, $"Confirm your {cfg.BrandName} email",
            $"{cfg.FrontendUrl}/confirm-email?userId={u.Id}&token={WebUtility.UrlEncode(token)}");
    }
    return Results.Ok(new { message = cfg.RequireEmailConfirmation ? "Check your email to confirm your account." : "Account created." });
});

auth.MapPost("/confirm-email", async (ConfirmReq r, UserManager<AppUser> um) =>
{
    var u = await um.FindByIdAsync(r.UserId);
    if (u is null || !(await um.ConfirmEmailAsync(u, r.Token)).Succeeded)
        return Results.BadRequest(new { error = "This confirmation link is invalid or expired." });
    return Results.Ok(new { message = "Email confirmed. You can log in." });
});

auth.MapPost("/login", async (LoginReq r, UserManager<AppUser> um) =>
{
    var u = await um.FindByEmailAsync(r.Email ?? "");
    if (u is null || await um.IsLockedOutAsync(u)) return Results.Json(new { error = "Incorrect email or password." }, statusCode: 401);
    if (!await um.CheckPasswordAsync(u, r.Password ?? ""))
    {
        await um.AccessFailedAsync(u);
        return Results.Json(new { error = "Incorrect email or password." }, statusCode: 401);
    }
    if (cfg.RequireEmailConfirmation && !u.EmailConfirmed)
        return Results.Json(new { error = "Confirm your email before logging in." }, statusCode: 403);
    await um.ResetAccessFailedCountAsync(u);
    var roles = await um.GetRolesAsync(u);
    var token = Helpers.MakeToken(u, roles, cfg);
    return Results.Ok(new { token, name = u.DisplayName, credits = u.Credits, plan = u.Plan, isAdmin = roles.Contains("Admin") });
});

auth.MapPost("/forgot-password", async (EmailReq r, UserManager<AppUser> um, IAppEmailSender mail) =>
{
    var u = await um.FindByEmailAsync(r.Email ?? "");
    if (u is not null)
    {
        var token = await um.GeneratePasswordResetTokenAsync(u);
        await mail.SendAsync(u.Email!, $"Reset your {cfg.BrandName} password",
            $"{cfg.FrontendUrl}/reset-password?email={WebUtility.UrlEncode(u.Email)}&token={WebUtility.UrlEncode(token)}");
    }
    return Results.Ok(new { message = "If that email has an account, a reset link is on its way." });
});

auth.MapPost("/reset-password", async (ResetReq r, UserManager<AppUser> um) =>
{
    var u = await um.FindByEmailAsync(r.Email ?? "");
    var ok = u is not null && (await um.ResetPasswordAsync(u, r.Token, r.NewPassword ?? "")).Succeeded;
    return ok ? Results.Ok(new { message = "Password updated." })
              : Results.BadRequest(new { error = "The reset link is invalid or expired, or the password is too weak." });
});

// ---------- Public ----------
app.MapGet("/api/tools", () => cfg.Tools.Where(t => t.Enabled).Select(t => new { t.Key, t.Name, t.Description, t.Cost }));
app.MapGet("/api/plans", () => cfg.Plans.Select(p => new { Name = p.Key, p.Value.MonthlyCredits, p.Value.PriceInr }));

// ---------- Signed-in user ----------
var api = app.MapGroup("/api").RequireAuthorization();
string Uid(ClaimsPrincipal p) => p.FindFirstValue(ClaimTypes.NameIdentifier)!;

api.MapGet("/me", async (ClaimsPrincipal p, AppDbContext db) =>
{
    var id = Uid(p);
    var me = await db.Users.Where(u => u.Id == id)
        .Select(u => new { u.Id, u.Email, u.DisplayName, u.Credits, u.Plan, u.PlanExpiresAt })
        .FirstOrDefaultAsync();
    return me is { } ? Results.Ok(me) : Results.NotFound();
});

api.MapPost("/ai/generate", async (GenReq r, ClaimsPrincipal p, AppDbContext db, AiService ai, ILogger<Program> log, CancellationToken ct) =>
{
    var tool = cfg.Tools.FirstOrDefault(t => t.Key == r.ToolKey && t.Enabled);
    if (tool is null) return Results.NotFound(new { error = "Tool not found." });
    if (string.IsNullOrWhiteSpace(r.Input) || r.Input.Length > 4000)
        return Results.BadRequest(new { error = "Write a request between 1 and 4,000 characters." });
    var template = (r.Template ?? "").Trim();
    if (template.Length > 100) template = template[..100];

    var id = Uid(p);
    var cost = tool.Cost;
    // Credits are checked and deducted on the server in one atomic step, before calling the AI.
    var charged = await db.Users.Where(u => u.Id == id && u.Credits >= cost)
        .ExecuteUpdateAsync(s => s.SetProperty(u => u.Credits, u => u.Credits - cost), ct);
    if (charged == 0) return Results.Json(new { error = "Not enough credits." }, statusCode: 402);

    try
    {
        var text = await ai.GenerateAsync(tool.SystemPrompt,
            $"Task type: {template}\n\nRequest: {r.Input}\n\nReply with the result only.", ct);
        db.Generations.Add(new Generation { UserId = id, ToolKey = tool.Key, Template = template, Input = r.Input, Output = text, CreditsUsed = cost });
        await db.SaveChangesAsync(ct);
        var left = await db.Users.Where(u => u.Id == id).Select(u => u.Credits).FirstAsync(ct);
        return Results.Ok(new { output = text, creditsLeft = left, creditsUsed = cost });
    }
    catch (Exception e)
    {
        log.LogError(e, "Generation failed for {Tool}", tool.Key);
        await db.Users.Where(u => u.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Credits, u => u.Credits + cost), CancellationToken.None);
        return Results.Json(new { error = "Generation failed. No credits were used." }, statusCode: 502);
    }
}).RequireRateLimiting("ai");

api.MapGet("/history", async (ClaimsPrincipal p, AppDbContext db, string? tool, int? take) =>
{
    var id = Uid(p);
    return await db.Generations.Where(g => g.UserId == id && (tool == null || g.ToolKey == tool))
        .OrderByDescending(g => g.CreatedAt).Take(Math.Clamp(take ?? 30, 1, 100))
        .Select(g => new { g.Id, g.ToolKey, g.Template, g.Input, g.Output, g.CreatedAt }).ToListAsync();
});

api.MapGet("/dashboard", async (ClaimsPrincipal p, AppDbContext db, CancellationToken ct) =>
{
    var id = Uid(p);
    var me = await db.Users.Where(u => u.Id == id).Select(u => new { u.DisplayName, u.Credits, u.Plan }).FirstAsync(ct);
    var total = await db.Generations.CountAsync(g => g.UserId == id, ct);
    var recent = await db.Generations.Where(g => g.UserId == id).OrderByDescending(g => g.CreatedAt).Take(5)
        .Select(g => new { g.Id, g.ToolKey, g.Input, g.CreatedAt }).ToListAsync(ct);
    var fav = await db.Generations.Where(g => g.UserId == id).GroupBy(g => g.ToolKey)
        .OrderByDescending(x => x.Count()).Select(x => x.Key).FirstOrDefaultAsync(ct);
    return Results.Ok(new
    {
        me.DisplayName, me.Credits, me.Plan, totalGenerations = total, favouriteTool = fav,
        recent = recent.Select(r => new { r.Id, r.ToolKey, Preview = r.Input.Length > 80 ? r.Input[..80] : r.Input, r.CreatedAt })
    });
});

// ---------- Admin (server-side role check) ----------
var admin = app.MapGroup("/api/admin").RequireAuthorization(p => p.RequireRole("Admin"));

admin.MapGet("/stats", async (AppDbContext db, CancellationToken ct) =>
{
    var since = DateTime.UtcNow.AddHours(-24);
    return Results.Ok(new
    {
        totalUsers = await db.Users.CountAsync(ct),
        paidUsers = await db.Users.CountAsync(u => u.Plan != "Free", ct),
        totalGenerations = await db.Generations.CountAsync(ct),
        generationsLast24h = await db.Generations.CountAsync(g => g.CreatedAt >= since, ct),
        creditsUsed = await db.Generations.SumAsync(g => (long)g.CreditsUsed, ct),
        revenueInr = (await db.Payments.Where(x => x.Status == "Paid").SumAsync(x => (long?)x.AmountPaise, ct) ?? 0) / 100
    });
});

admin.MapGet("/users", async (AppDbContext db, int? take) =>
    await db.Users.OrderByDescending(u => u.CreatedAt).Take(Math.Clamp(take ?? 50, 1, 200))
        .Select(u => new { u.Id, u.Email, u.DisplayName, u.Plan, u.Credits, u.EmailConfirmed, u.CreatedAt }).ToListAsync());

admin.MapPost("/users/{id}/credits", async (string id, GrantReq r, AppDbContext db) =>
{
    if (r.Amount is < -1_000_000 or > 1_000_000) return Results.BadRequest(new { error = "Amount out of range." });
    var n = await db.Users.Where(u => u.Id == id && u.Credits + r.Amount >= 0)
        .ExecuteUpdateAsync(s => s.SetProperty(u => u.Credits, u => u.Credits + r.Amount));
    return n == 0 ? Results.NotFound(new { error = "User not found or balance would go below zero." }) : Results.Ok(new { message = "Credits updated." });
});

app.MapBilling(cfg);

app.Run();
