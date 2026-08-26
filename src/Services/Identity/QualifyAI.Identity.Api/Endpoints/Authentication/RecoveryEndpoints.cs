using QualifyAI.Identity.Application.Authentication;
namespace QualifyAI.Identity.Api.Endpoints.Authentication;
public static class RecoveryEndpoints
{
    public static IEndpointRouteBuilder MapRecoveryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/account/forgot-password",async(ForgotRequest r,ITenantDirectoryClient tenants,IAccountService accounts,IHostEnvironment env,CancellationToken ct)=>{var t=await tenants.ResolveAsync(r.Tenant.Trim().ToLowerInvariant(),ct);if(t is null)return Results.Accepted();try{var token=await accounts.GeneratePasswordResetTokenAsync(t.Id,r.Email,ct);return env.IsDevelopment()?Results.Ok(new{resetToken=token}):Results.Accepted();}catch(KeyNotFoundException){return Results.Accepted();}}).AllowAnonymous();
        app.MapPost("/account/reset-password",async(ResetRequest r,ITenantDirectoryClient tenants,IAccountService accounts,CancellationToken ct)=>{var t=await tenants.ResolveAsync(r.Tenant.Trim().ToLowerInvariant(),ct);if(t is null)return Results.BadRequest(new{error="invalid_request"});try{await accounts.ResetPasswordAsync(t.Id,r.Email,r.Token,r.NewPassword,ct);return Results.NoContent();}catch{return Results.BadRequest(new{error="invalid_or_expired_token"});}}).AllowAnonymous();
        return app;
    }
}
public sealed record ForgotRequest(string Tenant,string Email);
public sealed record ResetRequest(string Tenant,string Email,string Token,string NewPassword);
