using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using QualifyAI.Infrastructure.Email;

namespace QualifyAI.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/public/demo-requests")]
public sealed class PublicDemoRequestsController(
    IEnumerable<IEmailDeliveryProvider> emailProviders,
    IConfiguration configuration,
    IMemoryCache cache) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(DemoRequestInput input, CancellationToken ct)
    {
        // Bots commonly fill fields that are hidden from real visitors.
        if (!string.IsNullOrWhiteSpace(input.Website))
            return Accepted(new { accepted = true });

        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 120 ||
            string.IsNullOrWhiteSpace(input.Email) || !input.Email.Contains('@') || input.Email.Length > 254 ||
            (!string.IsNullOrEmpty(input.Company) && input.Company.Length > 160) ||
            (!string.IsNullOrEmpty(input.Message) && input.Message.Length > 2_000))
            return BadRequest(new { detail = "Enter your name and a valid work email. Keep the request concise." });

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var throttleKey = "public-demo-request:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ip)));
        if (cache.TryGetValue(throttleKey, out _))
            return StatusCode(StatusCodes.Status429TooManyRequests, new { detail = "A demo request was already received from this network. Please try again in 15 minutes." });

        var recipient = configuration["Landing:DemoRequestRecipient"];
        var providerName = configuration["Email:Provider"];
        var fromEmail = configuration["Landing:DemoRequestFromEmail"];
        if (string.IsNullOrWhiteSpace(fromEmail)) fromEmail = configuration["Email:Smtp:Username"];
        var fromName = configuration["Landing:DemoRequestFromName"];
        if (string.IsNullOrWhiteSpace(fromName)) fromName = "Product team";
        if (string.IsNullOrWhiteSpace(recipient) || string.IsNullOrWhiteSpace(fromEmail) || string.IsNullOrWhiteSpace(providerName))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { detail = "Demo requests are not configured yet. Please try again shortly." });

        var provider = emailProviders.FirstOrDefault(x => x.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase));
        if (provider is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { detail = "Demo request email delivery is not enabled yet." });

        var name = WebUtility.HtmlEncode(input.Name.Trim());
        var email = WebUtility.HtmlEncode(input.Email.Trim());
        var company = WebUtility.HtmlEncode(input.Company?.Trim() ?? "Not provided");
        var message = WebUtility.HtmlEncode(input.Message?.Trim() ?? "No additional notes.").Replace("\n", "<br>");
        var result = await provider.SendAsync(new EmailEnvelope(
            fromEmail,
            fromName,
            recipient,
            "Product demo inbox",
            $"Demo request from {input.Name.Trim()}",
            $"<h2>New product demo request</h2><p><strong>Name:</strong> {name}</p><p><strong>Email:</strong> {email}</p><p><strong>Company:</strong> {company}</p><p><strong>Use case:</strong><br>{message}</p>",
            $"New product demo request\nName: {input.Name.Trim()}\nEmail: {input.Email.Trim()}\nCompany: {input.Company?.Trim() ?? "Not provided"}\nUse case: {input.Message?.Trim() ?? "No additional notes."}",
            Guid.NewGuid().ToString("N")), ct);

        if (!result.Success)
            return StatusCode(StatusCodes.Status502BadGateway, new { detail = "We could not deliver your request right now. Please try again later." });

        cache.Set(throttleKey, true, TimeSpan.FromMinutes(15));
        return Accepted(new { accepted = true });
    }
}

public sealed record DemoRequestInput(string Name, string Email, string? Company, string? Message, string? Website = null);
