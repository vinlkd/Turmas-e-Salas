using System.ComponentModel.DataAnnotations;
using Google.Cloud.Firestore;
using Salas_Elvis.Contracts;
using Salas_Elvis.Models;
using Salas_Elvis.Services;

var normal = new Sala { Id = "normal", Capacidade = 30 };
var alternativa = new Sala { Id = "alternativa", Capacidade = 30 };
var potente = new Sala { Id = "potente", Capacidade = 30, AltoDesempenho = true };
Sala[] salas = [normal, alternativa, potente];
var checks = 0;

Reserva R(string id, string sala, NivelUso nivel, int inicio = 9, int fim = 11, string status = "Confirmada")
    => new()
    {
        Id = id, SalaId = sala, Nivel = nivel, Status = status,
        Inicio = Timestamp.FromDateTime(new DateTime(2026, 10, 9, inicio, 0, 0, DateTimeKind.Utc)),
        Fim = Timestamp.FromDateTime(new DateTime(2026, 10, 9, fim, 0, 0, DateTimeKind.Utc))
    };
void Check(string name, Action test)
{
    test();
    checks++;
    Console.WriteLine($"OK: {name}");
}
void Assert(bool condition)
{
    if (!condition) throw new Exception("Verificação falhou.");
}
void Rejeita(int status, Action action)
{
    try { action(); }
    catch (ReservaException ex) when (ex.StatusCode == status) { return; }
    throw new Exception($"Esperado erro {status}.");
}

Check("Uso leve em sala convencional livre", () =>
    Assert(PlanejadorReservas.Planejar(R("nova", normal.Id, NivelUso.Leve), normal, salas, []).Count == 0));
Check("Uso pesado em sala de alto desempenho livre", () =>
    Assert(PlanejadorReservas.Planejar(R("nova", potente.Id, NivelUso.Pesado), potente, salas, []).Count == 0));
Check("Uso leve não ocupa sala de alto desempenho", () => Rejeita(400, () =>
    PlanejadorReservas.Planejar(R("nova", potente.Id, NivelUso.Leve), potente, salas, [])));
Check("Conflito entre reservas pesadas", () => Rejeita(409, () =>
    PlanejadorReservas.Planejar(R("nova", potente.Id, NivelUso.Pesado), potente, salas,
        [R("existente", potente.Id, NivelUso.Pesado)])));
Check("Reserva leve não desloca outra leve", () => Rejeita(409, () =>
    PlanejadorReservas.Planejar(R("nova", normal.Id, NivelUso.Leve), normal, salas,
        [R("existente", normal.Id, NivelUso.Leve)])));
Check("Reserva pesada remaneja reserva leve", () =>
{
    var movimentos = PlanejadorReservas.Planejar(R("nova", normal.Id, NivelUso.Pesado), normal,
        salas, [R("leve", normal.Id, NivelUso.Leve)]);
    Assert(movimentos.Count == 1 && movimentos[0].Destino.Id == alternativa.Id);
});
Check("Sem sala alternativa, reserva original é preservada", () =>
{
    var leve = R("leve", normal.Id, NivelUso.Leve);
    Rejeita(409, () => PlanejadorReservas.Planejar(R("nova", normal.Id, NivelUso.Pesado), normal,
        salas, [leve, R("ocupada", alternativa.Id, NivelUso.Pesado)]));
    Assert(leve.SalaId == normal.Id && leve.Status == "Confirmada");
});
Check("Remanejadas bloqueiam a sala de destino", () => Rejeita(409, () =>
    PlanejadorReservas.Planejar(R("nova", alternativa.Id, NivelUso.Leve), alternativa, salas,
        [R("movida", alternativa.Id, NivelUso.Leve, status: "Remanejada")])));
Check("Horários consecutivos não conflitam", () =>
    Assert(PlanejadorReservas.Planejar(R("nova", normal.Id, NivelUso.Leve, 11, 12), normal,
        salas, [R("existente", normal.Id, NivelUso.Pesado)]).Count == 0));
Check("Sobreposição parcial gera conflito", () => Rejeita(409, () =>
    PlanejadorReservas.Planejar(R("nova", normal.Id, NivelUso.Pesado, 10, 12), normal,
        salas, [R("existente", normal.Id, NivelUso.Pesado)])));
Check("Remanejamento verifica todo o período original", () => Rejeita(409, () =>
    PlanejadorReservas.Planejar(R("nova", normal.Id, NivelUso.Pesado, 10, 11), normal,
        salas, [R("leve", normal.Id, NivelUso.Leve, 9, 12), R("ocupada", alternativa.Id, NivelUso.Pesado, 11, 12)])));
Check("Sala menor não recebe turma remanejada", () => Rejeita(409, () =>
    PlanejadorReservas.Planejar(R("nova", normal.Id, NivelUso.Pesado), normal,
        [normal, new Sala { Id = "menor", Capacidade = 10 }], [R("leve", normal.Id, NivelUso.Leve)])));
Check("Múltiplos remanejamentos não criam sobreposição", () => Rejeita(409, () =>
    PlanejadorReservas.Planejar(R("nova", normal.Id, NivelUso.Pesado), normal, salas,
        [R("leve1", normal.Id, NivelUso.Leve), R("leve2", normal.Id, NivelUso.Leve)])));
Check("Múltiplos períodos consecutivos usam a mesma alternativa", () =>
    Assert(PlanejadorReservas.Planejar(R("nova", normal.Id, NivelUso.Pesado, 9, 12), normal, salas,
        [R("leve1", normal.Id, NivelUso.Leve, 9, 10), R("leve2", normal.Id, NivelUso.Leve, 10, 12)]).Count == 2));
Check("Período invertido é recusado", () => Rejeita(400, () =>
    PlanejadorReservas.Planejar(R("nova", normal.Id, NivelUso.Pesado, 12, 9), normal, salas, [])));
Check("Campos obrigatórios são validados", () =>
{
    var request = new CriarReservaRequest();
    Assert(!Validator.TryValidateObject(request, new ValidationContext(request), [], true));
});
Check("Fuso horário convertido corretamente para UTC", () =>
{
    var horario = new DateTimeOffset(2026, 10, 9, 9, 0, 0, TimeSpan.FromHours(-3));
    Assert(Timestamp.FromDateTimeOffset(horario).ToDateTime().Hour == 12);
});
Check("Coordenação pode reservar alto desempenho para uso comum", () =>
    Assert(PlanejadorReservas.Planejar(R("nova", potente.Id, NivelUso.Leve), potente, salas, [], administrador: true).Count == 0));
Check("Reserva cancelada libera a sala", () =>
    Assert(PlanejadorReservas.Planejar(R("nova", normal.Id, NivelUso.Leve), normal, salas,
        [R("anterior", normal.Id, NivelUso.Pesado, status: "Cancelada")]).Count == 0));
Check("Cancelamento libera destino de remanejamento", () =>
    Assert(PlanejadorReservas.Planejar(R("nova", normal.Id, NivelUso.Pesado), normal, salas,
        [R("leve", normal.Id, NivelUso.Leve), R("cancelada", alternativa.Id, NivelUso.Pesado, status: "Cancelada")]).Count == 1));
Check("Oficina não é remanejada para sala de aula", () =>
{
    var oficina = new Sala { Id = "oficina", Tipo = "Oficina", Capacidade = 20 };
    Rejeita(409, () => PlanejadorReservas.Planejar(R("nova", oficina.Id, NivelUso.Pesado), oficina,
        [oficina, normal], [R("leve", oficina.Id, NivelUso.Leve)]));
});
Console.WriteLine($"{checks} verificações passaram.");
