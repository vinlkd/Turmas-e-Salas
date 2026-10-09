using Google.Cloud.Firestore;
using Salas_Elvis.Contracts;
using Salas_Elvis.Models;

namespace Salas_Elvis.Services;

public class ReservaService(IEscolaStore store, TimeProvider timeProvider)
{
    public Task<IReadOnlyList<ReservaResponse>> ListarAsync(CancellationToken ct)
        => store.LerAsync<IReadOnlyList<ReservaResponse>>(dados => dados.Reservas
            .OrderBy(r => r.Inicio).Select(ReservaResponse.From).ToList(), ct);

    public Task<ReservaResponse> CriarAsync(CriarReservaRequest request, string usuarioId, CancellationToken ct)
        => store.AlterarAsync(dados =>
        {
            var usuario = dados.Usuarios.FirstOrDefault(u => u.Id == usuarioId)
                ?? throw new ReservaException(401, "Sua sessão expirou. Entre novamente.");
            var admin = usuario.Perfil == Perfis.Admin;
            var sala = dados.Salas.FirstOrDefault(s => s.Id == request.SalaId)
                ?? throw new ReservaException(404, "Sala não encontrada.");
            if (!dados.Turmas.Any(t => t.Id == request.TurmaId))
                throw new ReservaException(404, "Turma não encontrada.");
            var professorId = string.IsNullOrWhiteSpace(request.ProfessorId) ? usuario.Id : request.ProfessorId;
            if (!admin && professorId != usuario.Id)
                throw new ReservaException(403, "O professor só pode fazer reservas em seu próprio nome.");
            if (!dados.Usuarios.Any(u => u.Id == professorId))
                throw new ReservaException(404, "Responsável não encontrado.");
            var nova = new Reserva
            {
                Id = Guid.NewGuid().ToString("N"), SalaId = sala.Id, ProfessorId = professorId,
                TurmaId = request.TurmaId, CriadoPorId = usuario.Id,
                Atividade = request.Atividade.Trim(), Nivel = request.Nivel!.Value,
                Inicio = Timestamp.FromDateTimeOffset(request.Inicio!.Value),
                Fim = Timestamp.FromDateTimeOffset(request.Fim!.Value)
            };
            if (dados.Reservas.Any(r => r.Status != "Cancelada" && r.TurmaId == nova.TurmaId
                && PlanejadorReservas.Sobrepoe(r, nova)))
                throw new ReservaException(409, "Essa turma já tem uma reserva nesse período. A coordenação pode cancelar a reserva anterior.");
            var remanejamentos = PlanejadorReservas.Planejar(nova, sala, dados.Salas, dados.Reservas, admin);
            foreach (var movimento in remanejamentos)
            {
                movimento.Reserva.SalaId = movimento.Destino.Id;
                movimento.Reserva.Status = "Remanejada";
            }
            dados.Reservas.Add(nova);
            return ReservaResponse.From(nova);
        }, ct);

    public Task<ReservaResponse> CancelarAsync(string id, string motivo, string usuarioId, CancellationToken ct)
        => store.AlterarAsync(dados =>
        {
            if (!dados.Usuarios.Any(u => u.Id == usuarioId && u.Perfil == Perfis.Admin))
                throw new ReservaException(403, "Somente a coordenação pode cancelar reservas.");
            var reserva = dados.Reservas.FirstOrDefault(r => r.Id == id)
                ?? throw new ReservaException(404, "Reserva não encontrada.");
            if (reserva.Status == "Cancelada")
                throw new ReservaException(409, "Essa reserva já foi cancelada.");
            var limiteCancelamento = reserva.Inicio.ToDateTimeOffset().AddMinutes(-30);
            if (timeProvider.GetUtcNow() > limiteCancelamento)
                throw new ReservaException(409, "O prazo para cancelar essa reserva já terminou. O cancelamento só pode ser feito até 30 minutos antes do início.");
            reserva.Status = "Cancelada";
            reserva.CanceladoPorId = usuarioId;
            reserva.CanceladoEm = Timestamp.GetCurrentTimestamp();
            reserva.MotivoCancelamento = motivo.Trim();
            return ReservaResponse.From(reserva);
        }, ct);
}
