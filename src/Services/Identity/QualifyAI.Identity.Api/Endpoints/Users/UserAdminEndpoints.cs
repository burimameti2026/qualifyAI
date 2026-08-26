using System.Security.Claims;
using QualifyAI.BuildingBlocks.Security.Claims;
using QualifyAI.Identity.Application.Authentication;
namespace QualifyAI.Identity.Api.Endpoints.Users;
public static class UserAdminEndpoints
{
    public static IEndpointRouteBuilder MapUserAdmin(this IEndpointRouteBuilder app)
    {
        var g=app.MapGroup("/users").RequireAuthorization();
        g.MapGet("/",async(HttpContext ctx,IAccountService accounts,CancellationToken ct)=>{if(!TryTenant(ctx,out var t))return Results.Unauthorized();return Results.Ok(await accounts.ListUsersAsync(t,ct));});
        g.MapGet("/me",async(HttpContext ctx,IAccountService accounts,CancellationToken ct)=>{if(!TryTenant(ctx,out var t)||!TryUser(ctx,out var id))return Results.Unauthorized();var x=await accounts.GetUserAsync(t,id,ct);return x is null?Results.NotFound():Results.Ok(x);});
        g.MapGet("/{id:guid}",async(Guid id,HttpContext ctx,IAccountService accounts,CancellationToken ct)=>{if(!TryTenant(ctx,out var t))return Results.Unauthorized();var x=await accounts.GetUserAsync(t,id,ct);return x is null?Results.NotFound():Results.Ok(x);});
        g.MapPost("/",async(CreateUserRequest r,HttpContext ctx,IAccountService accounts,CancellationToken ct)=>{if(!TryTenant(ctx,out var t))return Results.Unauthorized();var slug=ctx.User.FindFirst(QualifyAiClaimTypes.TenantSlug)?.Value??"";return Results.Ok(await accounts.CreateUserAsync(new(t,slug,r.Email,r.Password,r.FirstName,r.LastName,r.Roles??[]),ct));});
        g.MapPut("/{id:guid}/roles",async(Guid id,RolesRequest r,HttpContext ctx,IAccountService accounts,CancellationToken ct)=>{if(!TryTenant(ctx,out var t))return Results.Unauthorized();await accounts.SetRolesAsync(t,id,r.Roles,ct);return Results.NoContent();});
        g.MapPut("/{id:guid}/permissions",async(Guid id,PermissionsRequest r,HttpContext ctx,IAccountService accounts,CancellationToken ct)=>{if(!TryTenant(ctx,out var t))return Results.Unauthorized();await accounts.SetPermissionsAsync(t,id,r.Permissions,ct);return Results.NoContent();});
        g.MapPost("/{id:guid}/disable",async(Guid id,HttpContext ctx,IAccountService accounts,CancellationToken ct)=>{if(!TryTenant(ctx,out var t))return Results.Unauthorized();await accounts.DisableAsync(t,id,ct);return Results.NoContent();});
        g.MapPost("/{id:guid}/enable",async(Guid id,HttpContext ctx,IAccountService accounts,CancellationToken ct)=>{if(!TryTenant(ctx,out var t))return Results.Unauthorized();await accounts.EnableAsync(t,id,ct);return Results.NoContent();});
        g.MapPost("/me/change-password",async(ChangePasswordRequest r,HttpContext ctx,IAccountService accounts,CancellationToken ct)=>{if(!TryTenant(ctx,out var t)||!TryUser(ctx,out var id))return Results.Unauthorized();await accounts.ChangePasswordAsync(t,id,r.CurrentPassword,r.NewPassword,ct);return Results.NoContent();});
        g.MapPost("/me/mfa/setup",async(HttpContext ctx,IAccountService accounts,CancellationToken ct)=>{if(!TryTenant(ctx,out var t)||!TryUser(ctx,out var id))return Results.Unauthorized();return Results.Ok(await accounts.BeginMfaAsync(t,id,ct));});
        g.MapPost("/me/mfa/confirm",async(MfaCodeRequest r,HttpContext ctx,IAccountService accounts,CancellationToken ct)=>{if(!TryTenant(ctx,out var t)||!TryUser(ctx,out var id))return Results.Unauthorized();return await accounts.ConfirmMfaAsync(t,id,r.Code,ct)?Results.NoContent():Results.BadRequest(new{error="invalid_code"});});
        g.MapDelete("/me/mfa",async(HttpContext ctx,IAccountService accounts,CancellationToken ct)=>{if(!TryTenant(ctx,out var t)||!TryUser(ctx,out var id))return Results.Unauthorized();await accounts.DisableMfaAsync(t,id,ct);return Results.NoContent();});
        return app;
    }
    private static bool TryTenant(HttpContext ctx,out Guid id)=>Guid.TryParse(ctx.User.FindFirst(QualifyAiClaimTypes.TenantId)?.Value,out id);
    private static bool TryUser(HttpContext ctx,out Guid id)=>Guid.TryParse(ctx.User.FindFirstValue("sub"),out id);
}
public sealed record CreateUserRequest(string Email,string Password,string FirstName,string LastName,string[]? Roles);
public sealed record RolesRequest(string[] Roles);
public sealed record PermissionsRequest(string[] Permissions);
public sealed record ChangePasswordRequest(string CurrentPassword,string NewPassword);
public sealed record MfaCodeRequest(string Code);
