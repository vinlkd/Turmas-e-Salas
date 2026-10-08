using Google.Cloud.Firestore;

namespace Salas_Elvis.Models;

[FirestoreData]
public class Sala
{
    [FirestoreDocumentId] public string Id { get; set; } = "";
    [FirestoreProperty] public string Nome { get; set; } = "";
    [FirestoreProperty] public int Capacidade { get; set; }
    [FirestoreProperty] public bool AltoDesempenho { get; set; }
    [FirestoreProperty] public string Tipo { get; set; } = "Sala de aula";
    [FirestoreProperty] public string Localizacao { get; set; } = "";
}
