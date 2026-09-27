using System.Security.Cryptography; using System.Text;
using Microsoft.AspNetCore.Identity; using QualifyAI.Application; using QualifyAI.Domain;
namespace QualifyAI.Infrastructure;
public sealed class PasswordService:IPasswordService { readonly PasswordHasher<AppUser> _h=new(); public string Hash(string v)=>_h.HashPassword(new(),v); public bool Verify(string h,string v)=>_h.VerifyHashedPassword(new(),h,v)!=PasswordVerificationResult.Failed; }
public static class Crypto { public static string Sha256(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))); }
