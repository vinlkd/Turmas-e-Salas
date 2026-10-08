using System.Text.Json;
using System.Text.Json.Serialization;
using Google.Cloud.Firestore;
using Salas_Elvis.Models;

namespace Salas_Elvis.Services;

// Armazenamento persistente para uma única instância do protótipo em desenvolvimento.
public sealed class LocalDemoStore : IEscolaStore
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly string path;
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true, Converters = { new TimestampConverter() }
    };

    public LocalDemoStore(string path) => this.path = path;

    public Task<T> LerAsync<T>(Func<DadosEscola, T> consulta, CancellationToken ct = default)
        => ExecutarAsync(consulta, false, ct);
    public Task<T> AlterarAsync<T>(Func<DadosEscola, T> operacao, CancellationToken ct = default)
        => ExecutarAsync(operacao, true, ct);

    private async Task<T> ExecutarAsync<T>(Func<DadosEscola, T> operacao, bool salvar, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            var existe = File.Exists(path);
            var dados = existe ? JsonSerializer.Deserialize<DadosEscola>(await File.ReadAllTextAsync(path, ct), Json)!
                : CriarDemonstracao();
            var resultado = operacao(dados);
            if (salvar || !existe)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(path + ".tmp", JsonSerializer.Serialize(dados, Json), ct);
                File.Move(path + ".tmp", path, overwrite: true);
            }
            return resultado;
        }
        finally { gate.Release(); }
    }

    private static DadosEscola CriarDemonstracao() => new()
    {
        Usuarios =
        [
            new() { Id = "admin-demo", Nome = "Coordenação SENAI", Email = "admin@senai.demo", Perfil = Perfis.Admin, SenhaHash = BCrypt.Net.BCrypt.HashPassword("Admin@123") },
            new() { Id = "professor-demo", Nome = "Professor Elvis", Email = "professor@senai.demo", Perfil = Perfis.Professor, SenhaHash = BCrypt.Net.BCrypt.HashPassword("Professor@123") }
        ],
        Salas =
        [
            new() { Id = "sala-101", Nome = "Sala 101", Tipo = "Sala de aula", Localizacao = "Bloco A · 1º andar", Capacidade = 40 },
            new() { Id = "lab-cad", Nome = "Laboratório CAD", Tipo = "Informática", Localizacao = "Bloco B · Térreo", Capacidade = 30, AltoDesempenho = true },
            new() { Id = "oficina", Nome = "Oficina de mecânica", Tipo = "Oficina", Localizacao = "Bloco C · Térreo", Capacidade = 25 },
            new() { Id = "auditorio", Nome = "Auditório", Tipo = "Auditório", Localizacao = "Bloco A · Térreo", Capacidade = 120 }
        ],
        Turmas =
        [
            new() { Id = "dev-2026", Nome = "DEV 2026.1", Curso = "Desenvolvimento de Sistemas", Turno = "Manhã", QuantidadeAlunos = 30 },
            new() { Id = "mec-2026", Nome = "MEC 2026.1", Curso = "Mecânica", Turno = "Tarde", QuantidadeAlunos = 25 }
        ]
    };

    private sealed class TimestampConverter : JsonConverter<Timestamp>
    {
        public override Timestamp Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
            => Timestamp.FromDateTimeOffset(reader.GetDateTimeOffset());
        public override void Write(Utf8JsonWriter writer, Timestamp value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToDateTimeOffset());
    }
}
