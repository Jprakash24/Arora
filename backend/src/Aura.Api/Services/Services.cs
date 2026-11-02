using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace Aura.Api;

public interface IAppEmailSender { Task SendAsync(string to, string subject, string body); }

// Development sender: writes the email to the log. Replace with SMTP/Resend/SendGrid before launch.
public class LogEmailSender(ILogger<LogEmailSender> log) : IAppEmailSender
{
    public Task SendAsync(string to, string subject, string body)
    {
        log.LogWarning("EMAIL to {To} | {Subject}\n{Body}", to, subject, body);
        return Task.CompletedTask;
    }
}

public class AiService(HttpClient http, AuraOptions o, ILogger<AiService> log)
{
    public async Task<string> GenerateAsync(string system, string prompt, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(o.Ai.ApiKey))
            throw new InvalidOperationException("Aura:Ai:ApiKey is not configured.");

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        req.Headers.Add("x-api-key", o.Ai.ApiKey);
        req.Headers.Add("anthropic-version", "2023-06-01");
        req.Content = JsonContent.Create(new
        {
            model = o.Ai.Model,
            max_tokens = o.Ai.MaxTokens,
            system,
            messages = new[] { new { role = "user", content = prompt } }
        });

        using var res = await http.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            log.LogError("AI provider returned {Status}: {Body}", (int)res.StatusCode, body);
            throw new HttpRequestException("AI provider error");
        }

        using var doc = JsonDocument.Parse(body);
        var sb = new StringBuilder();
        foreach (var block in doc.RootElement.GetProperty("content").EnumerateArray())
            if (block.GetProperty("type").GetString() == "text")
                sb.Append(block.GetProperty("text").GetString());
        return sb.ToString();
    }
}

public static class Helpers
{
    public static string MakeToken(AppUser u, IList<string> roles, AuraOptions o)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, u.Id),
            new(ClaimTypes.Email, u.Email ?? "")
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.Jwt.Key));
        var token = new JwtSecurityToken(o.Jwt.Issuer, o.Jwt.Audience, claims,
            expires: DateTime.UtcNow.AddHours(o.Jwt.Hours),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static bool IsEmail(string? e) =>
        !string.IsNullOrWhiteSpace(e) && e.Length <= 254 && System.Net.Mail.MailAddress.TryCreate(e, out _);
}
