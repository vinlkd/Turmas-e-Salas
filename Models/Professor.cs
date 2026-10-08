using Google.Cloud.Firestore;

namespace Salas_Elvis.Models;

[FirestoreData]
public class Professor
{
    [FirestoreDocumentId] public string Id { get; set; } = "";
    [FirestoreProperty] public string Nome { get; set; } = "";
    [FirestoreProperty] public string Email { get; set; } = "";
}
