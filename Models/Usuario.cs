using Google.Cloud.Firestore;

namespace Salas_Elvis.Models;

public static class Perfis
{
    public const string Admin = "Admin";
    public const string Professor = "Professor";
}

[FirestoreData]
public class Usuario
{
    [FirestoreDocumentId] public string Id { get; set; } = "";
    [FirestoreProperty] public string Nome { get; set; } = "";
    [FirestoreProperty] public string Email { get; set; } = "";
    [FirestoreProperty] public string SenhaHash { get; set; } = "";
    [FirestoreProperty] public string Perfil { get; set; } = Perfis.Professor;
}
