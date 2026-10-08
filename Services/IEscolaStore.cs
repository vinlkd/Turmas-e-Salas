using Salas_Elvis.Models;

namespace Salas_Elvis.Services;

public class DadosEscola
{
    public List<Sala> Salas { get; set; } = [];
    public List<Turma> Turmas { get; set; } = [];
    public List<Usuario> Usuarios { get; set; } = [];
    public List<Reserva> Reservas { get; set; } = [];
}

public interface IEscolaStore
{
    Task<T> LerAsync<T>(Func<DadosEscola, T> consulta, CancellationToken ct = default);
    Task<T> AlterarAsync<T>(Func<DadosEscola, T> operacao, CancellationToken ct = default);
}
