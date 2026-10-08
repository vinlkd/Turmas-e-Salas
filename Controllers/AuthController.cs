using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Salas_Elvis.Contracts;
using Salas_Elvis.Models;
using Salas_Elvis.Services;

namespace Salas_Elvis.Controllers;

[ApiController]
[Route("auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AuthController(IEscolaStore store, IAntiforgery antiforgery, ModoAplicacao modo) : ControllerBase
{
    // Hash de custo equivalente para não distinguir e-mails inexistentes pelo tempo de resposta.
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString());

    [HttpGet("sessao")]
    public IActionResult Sessao() => Ok(new
    {
        usuario = User.Identity?.IsAuthenticated == true ? new
        {
            id = User.FindFirstValue(ClaimTypes.NameIdentifier),
            nome = User.Identity.Name,
            perfil = User.FindFirstValue(ClaimTypes.Role)
        } : null,
        csrfToken = antiforgery.GetAndStoreTokens(HttpContext).RequestToken,
        demonstracao = modo.Demonstracao
    });

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var usuario = await store.LerAsync(d => d.Usuarios.FirstOrDefault(u =>
            string.Equals(u.Email, request.Email.Trim(), StringComparison.OrdinalIgnoreCase)), ct);
        var valida = BCrypt.Net.BCrypt.Verify(request.Senha, usuario?.SenhaHash ?? DummyHash);
        if (!valida || usuario is null || usuario.Perfil is not (Perfis.Admin or Perfis.Professor))
            return Unauthorized(new ProblemDetails { Status = 401, Detail = "E-mail ou senha incorretos." });
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.Id), new Claim(ClaimTypes.Name, usuario.Nome),
            new Claim(ClaimTypes.Role, usuario.Perfil)
        }, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return Ok(new { usuario.Id, usuario.Nome, usuario.Perfil });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }
}
