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
        await EntrarAsync(usuario);
        return Ok(new { usuario.Id, usuario.Nome, usuario.Perfil });
    }

    [Authorize]
    [HttpGet("conta")]
    public async Task<IActionResult> Conta(CancellationToken ct)
    {
        var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var usuario = await store.LerAsync(d => d.Usuarios.FirstOrDefault(u => u.Id == usuarioId), ct)
            ?? throw new ReservaException(401, "Sua sessão expirou. Entre novamente.");
        return Ok(new { usuario.Id, usuario.Nome, usuario.Email, usuario.Perfil });
    }

    [Authorize]
    [EnableRateLimiting("conta")]
    [HttpPut("conta")]
    public async Task<IActionResult> EditarConta(EditarContaRequest request, CancellationToken ct)
    {
        var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var atualizado = await store.AlterarAsync(d =>
        {
            var usuario = d.Usuarios.FirstOrDefault(u => u.Id == usuarioId)
                ?? throw new ReservaException(401, "Sua sessão expirou. Entre novamente.");
            var email = request.Email.Trim().ToLowerInvariant();
            var alteraEmail = !string.Equals(email, usuario.Email, StringComparison.OrdinalIgnoreCase);
            var alteraSenha = !string.IsNullOrEmpty(request.NovaSenha);
            if ((alteraEmail || alteraSenha) && (string.IsNullOrEmpty(request.SenhaAtual)
                || !BCrypt.Net.BCrypt.Verify(request.SenhaAtual, usuario.SenhaHash)))
                throw new ReservaException(400, "Informe sua senha atual corretamente para alterar o e-mail ou a senha.");
            if (d.Usuarios.Any(u => u.Id != usuarioId && string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)))
                throw new ReservaException(409, "Esse e-mail já está sendo usado por outra conta.");
            var senhaHash = alteraSenha ? BCrypt.Net.BCrypt.HashPassword(request.NovaSenha!) : usuario.SenhaHash;
            usuario.Nome = request.Nome.Trim();
            usuario.Email = email;
            usuario.SenhaHash = senhaHash;
            return usuario;
        }, ct);
        // O perfil e o ID vêm da conta autenticada, nunca do corpo enviado pelo navegador.
        await EntrarAsync(atualizado);
        return Ok(new { atualizado.Id, atualizado.Nome, atualizado.Email, atualizado.Perfil });
    }

    private Task EntrarAsync(Usuario usuario)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.Id), new Claim(ClaimTypes.Name, usuario.Nome),
            new Claim(ClaimTypes.Role, usuario.Perfil)
        }, CookieAuthenticationDefaults.AuthenticationScheme);
        return HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }
}
