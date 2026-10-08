using Salas_Elvis.Models;

namespace Salas_Elvis.Services;

public record Remanejamento(Reserva Reserva, Sala Destino);

public static class PlanejadorReservas
{
    public static IReadOnlyList<Remanejamento> Planejar(Reserva nova, Sala sala,
        IReadOnlyList<Sala> salas, IReadOnlyList<Reserva> reservas, bool administrador = false)
    {
        if (!Enum.IsDefined(nova.Nivel) || nova.Fim <= nova.Inicio)
            throw new ReservaException(400, "Nível ou período da reserva inválido.");
        if (!administrador && sala.AltoDesempenho && nova.Nivel == NivelUso.Leve)
            throw new ReservaException(400, "Uso leve deve reservar uma sala convencional.");

        // Reservas remanejadas continuam ocupando o horário na sala de destino.
        reservas = reservas.Where(r => r.Status != "Cancelada").ToList();
        var conflitos = reservas.Where(r => r.SalaId == sala.Id && Sobrepoe(r, nova)).ToList();
        if (conflitos.Count == 0) return [];
        if (nova.Nivel == NivelUso.Leve || conflitos.Any(r => r.Nivel == NivelUso.Pesado))
            throw new ReservaException(409, "A sala já está reservada nesse período.");

        var resultado = new List<Remanejamento>();
        foreach (var conflito in conflitos.OrderBy(r => r.Inicio).ThenBy(r => r.Id))
        {
            var destino = salas.Where(s => s.Id != sala.Id && !s.AltoDesempenho && s.Tipo == sala.Tipo
                    && s.Capacidade >= sala.Capacidade)
                .OrderBy(s => s.Capacidade).ThenBy(s => s.Id)
                .FirstOrDefault(s => !reservas.Any(r => r.SalaId == s.Id && Sobrepoe(r, conflito))
                    && !resultado.Any(m => m.Destino.Id == s.Id && Sobrepoe(m.Reserva, conflito)));
            if (destino is null)
                throw new ReservaException(409, "Não há sala convencional disponível com capacidade suficiente para remanejar a reserva leve.");
            resultado.Add(new Remanejamento(conflito, destino));
        }
        return resultado;
    }

    public static bool Sobrepoe(Reserva a, Reserva b) => a.Inicio < b.Fim && a.Fim > b.Inicio;
}
