using Google.Cloud.Firestore;
using Salas_Elvis.Models;
using System.Text.Json;

namespace Salas_Elvis.Services;

// Snapshot pequeno para o protótipo. O documento de controle serializa as alterações.
public class FirestoreEscolaStore(FirestoreDb db) : IEscolaStore
{
    public async Task<T> LerAsync<T>(Func<DadosEscola, T> consulta, CancellationToken ct = default)
        => consulta(await CarregarAsync(q => q.GetSnapshotAsync(ct)));

    public Task<T> AlterarAsync<T>(Func<DadosEscola, T> operacao, CancellationToken ct = default)
        => db.RunTransactionAsync(async tx =>
        {
            var controle = db.Collection("controle").Document("escola");
            await tx.GetSnapshotAsync(controle, ct);
            var dados = await CarregarAsync(q => tx.GetSnapshotAsync(q, ct));
            var antes = Documentos(dados).ToDictionary(d => d.Path, d => JsonSerializer.Serialize(d.Value, d.Value.GetType()));
            var resultado = operacao(dados);
            // Leituras completas antes de qualquer escrita. Cancelamentos e novas reservas são atômicos.
            tx.Set(controle, new Dictionary<string, object> { ["Versao"] = FieldValue.Increment(1) }, SetOptions.MergeAll);
            foreach (var documento in Documentos(dados))
                if (!antes.TryGetValue(documento.Path, out var original)
                    || original != JsonSerializer.Serialize(documento.Value, documento.Value.GetType()))
                    tx.Set(db.Document(documento.Path), documento.Value, SetOptions.MergeAll);
            return resultado;
        }, cancellationToken: ct);

    private static IEnumerable<(string Path, object Value)> Documentos(DadosEscola dados)
    {
        foreach (var s in dados.Salas) yield return ($"salas/{s.Id}", s);
        foreach (var t in dados.Turmas) yield return ($"turmas/{t.Id}", t);
        foreach (var u in dados.Usuarios) yield return ($"usuarios/{u.Id}", u);
        foreach (var r in dados.Reservas) yield return ($"reservas/{r.Id}", r);
    }

    private async Task<DadosEscola> CarregarAsync(Func<Query, Task<QuerySnapshot>> ler)
    {
        var salas = await ler(db.Collection("salas"));
        var turmas = await ler(db.Collection("turmas"));
        var usuarios = await ler(db.Collection("usuarios"));
        var reservas = await ler(db.Collection("reservas"));
        return new DadosEscola
        {
            Salas = salas.Documents.Select(d => d.ConvertTo<Sala>()).ToList(),
            Turmas = turmas.Documents.Select(d => d.ConvertTo<Turma>()).ToList(),
            Usuarios = usuarios.Documents.Select(d => d.ConvertTo<Usuario>()).ToList(),
            Reservas = reservas.Documents.Select(d => d.ConvertTo<Reserva>()).ToList()
        };
    }
}
