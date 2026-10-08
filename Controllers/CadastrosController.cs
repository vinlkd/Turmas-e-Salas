using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Salas_Elvis.Contracts;
using Salas_Elvis.Models;
using Salas_Elvis.Services;

namespace Salas_Elvis.Controllers;

[ApiController]
[Authorize]
public class CadastrosController(IEscolaStore store) : ControllerBase
{
    [HttpGet("salas")]
    public async Task<IActionResult> Salas(CancellationToken ct)
        => Ok(await store.LerAsync(d => d.Salas.OrderBy(s => s.Nome).ToList(), ct));

    [HttpGet("turmas")]
    public async Task<IActionResult> Turmas(CancellationToken ct)
        => Ok(await store.LerAsync(d => d.Turmas.OrderBy(t => t.Nome).ToList(), ct));

    [HttpGet("professores")]
    public async Task<IActionResult> Professores(CancellationToken ct)
        => Ok(await store.LerAsync(d => d.Usuarios.OrderBy(u => u.Nome)
            .Select(u => new { u.Id, u.Nome, u.Perfil }).ToList(), ct));

    [HttpPost("salas")]
    [Authorize(Roles = Perfis.Admin)]
    public async Task<IActionResult> CriarSala(CriarSalaRequest request, CancellationToken ct)
    {
        var sala = await store.AlterarAsync(d =>
        {
            if (d.Salas.Any(s => string.Equals(s.Nome, request.Nome.Trim(), StringComparison.OrdinalIgnoreCase)))
                throw new ReservaException(409, "Já existe uma sala com esse nome.");
            var nova = new Sala
            {
                Id = Guid.NewGuid().ToString("N"),
                Nome = request.Nome.Trim(),
                Capacidade = request.Capacidade,
                Tipo = request.Tipo,
                Localizacao = (request.Localizacao ?? "").Trim(),
                AltoDesempenho = request.AltoDesempenho
            };
            d.Salas.Add(nova);
            return nova;
        }, ct);
        return StatusCode(201, sala);
    }

    [HttpPost("turmas")]
    [Authorize(Roles = Perfis.Admin)]
    public async Task<IActionResult> CriarTurma(CriarTurmaRequest request, CancellationToken ct)
    {
        var turma = await store.AlterarAsync(d =>
        {
            if (d.Turmas.Any(t => string.Equals(t.Nome, request.Nome.Trim(), StringComparison.OrdinalIgnoreCase)))
                throw new ReservaException(409, "Já existe uma turma com esse nome.");
            var nova = new Turma
            {
                Id = Guid.NewGuid().ToString("N"),
                Nome = request.Nome.Trim(),
                Curso = request.Curso.Trim(),
                Turno = request.Turno,
                QuantidadeAlunos = request.QuantidadeAlunos
            };
            d.Turmas.Add(nova);
            return nova;
        }, ct);
        return StatusCode(201, turma);
    }

    [HttpPut("salas/{id}")]
    [Authorize(Roles = Perfis.Admin)]
    public async Task<IActionResult> EditarSala(string id, CriarSalaRequest request, CancellationToken ct)
    {
        var sala = await store.AlterarAsync(d =>
        {
            var atual = d.Salas.FirstOrDefault(s => s.Id == id)
                ?? throw new ReservaException(404, "Sala não encontrada.");
            if (d.Salas.Any(s => s.Id != id && string.Equals(s.Nome, request.Nome.Trim(), StringComparison.OrdinalIgnoreCase)))
                throw new ReservaException(409, "Já existe uma sala com esse nome.");
            atual.Nome = request.Nome.Trim();
            atual.Capacidade = request.Capacidade;
            atual.Tipo = request.Tipo;
            atual.Localizacao = (request.Localizacao ?? "").Trim();
            atual.AltoDesempenho = request.AltoDesempenho;
            return atual;
        }, ct);
        return Ok(sala);
    }

    [HttpPut("turmas/{id}")]
    [Authorize(Roles = Perfis.Admin)]
    public async Task<IActionResult> EditarTurma(string id, CriarTurmaRequest request, CancellationToken ct)
    {
        var turma = await store.AlterarAsync(d =>
        {
            var atual = d.Turmas.FirstOrDefault(t => t.Id == id)
                ?? throw new ReservaException(404, "Turma não encontrada.");
            if (d.Turmas.Any(t => t.Id != id && string.Equals(t.Nome, request.Nome.Trim(), StringComparison.OrdinalIgnoreCase)))
                throw new ReservaException(409, "Já existe uma turma com esse nome.");
            atual.Nome = request.Nome.Trim();
            atual.Curso = request.Curso.Trim();
            atual.Turno = request.Turno;
            atual.QuantidadeAlunos = request.QuantidadeAlunos;
            return atual;
        }, ct);
        return Ok(turma);
    }
}
