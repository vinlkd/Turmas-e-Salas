using Google.Cloud.Firestore;

namespace Salas_Elvis.Models;

[FirestoreData]
public class Turma
{
    [FirestoreDocumentId] public string Id { get; set; } = "";
    [FirestoreProperty] public string Nome { get; set; } = "";
    [FirestoreProperty] public string Curso { get; set; } = "";
    [FirestoreProperty] public string Turno { get; set; } = "";
    [FirestoreProperty] public int QuantidadeAlunos { get; set; }
}
