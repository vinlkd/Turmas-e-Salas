using Salas_Elvis.Models;

namespace Salas_Elvis.Contracts;

public record ReservaResponse(string Id, string SalaId, string ProfessorId,
    string Atividade, NivelUso Nivel, DateTimeOffset Inicio, DateTimeOffset Fim, string Status,
    string TurmaId, string CriadoPorId, string MotivoCancelamento, DateTimeOffset? CanceladoEm)
{
    public static ReservaResponse From(Reserva reserva) => new(reserva.Id, reserva.SalaId,
        reserva.ProfessorId, reserva.Atividade, reserva.Nivel,
        reserva.Inicio.ToDateTimeOffset(), reserva.Fim.ToDateTimeOffset(), reserva.Status,
        reserva.TurmaId, reserva.CriadoPorId, reserva.MotivoCancelamento, reserva.CanceladoEm?.ToDateTimeOffset());
}
