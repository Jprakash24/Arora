namespace Aura.Api;

// Central configuration. Everything here is editable in appsettings.json (section "Aura").
public class AuraOptions
{
    public string BrandName { get; set; } = "AURA";
    public string FrontendUrl { get; set; } = "http://localhost:5173";
    public string[] CorsOrigins { get; set; } = [];
    public bool RequireEmailConfirmation { get; set; } = true;
    public string AdminEmail { get; set; } = "";
    public string AdminPassword { get; set; } = "";
    public AiOptions Ai { get; set; } = new();
    public JwtOptions Jwt { get; set; } = new();
    public RazorpayOptions Razorpay { get; set; } = new();
    public Dictionary<string, PlanOptions> Plans { get; set; } = new();
    public List<ToolOptions> Tools { get; set; } = new();
}

public class AiOptions
{
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "claude-sonnet-5-5";
    public int MaxTokens { get; set; } = 1500;
}

public class JwtOptions
{
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "aura-api";
    public string Audience { get; set; } = "aura-web";
    public int Hours { get; set; } = 12;
}

public class RazorpayOptions
{
    public string KeyId { get; set; } = "";
    public string KeySecret { get; set; } = "";
    public string WebhookSecret { get; set; } = "";
}

public class PlanOptions
{
    public int MonthlyCredits { get; set; }
    public decimal PriceInr { get; set; }
}

public class ToolOptions
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Cost { get; set; } = 1;
    public bool Enabled { get; set; } = true;
    public string SystemPrompt { get; set; } = "";
}

public record SignupReq(string Email, string Password, string? Name);
public record LoginReq(string Email, string Password);
public record EmailReq(string Email);
public record ConfirmReq(string UserId, string Token);
public record ResetReq(string Email, string Token, string NewPassword);
public record GenReq(string ToolKey, string? Template, string Input);
public record GrantReq(int Amount);
public record CheckoutReq(string Plan);
