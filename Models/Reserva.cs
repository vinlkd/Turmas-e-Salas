using Google.Cloud.Firestore;

namespace Salas_Elvis.Models;

public enum NivelUso { Leve, Pesado }

[FirestoreData]
public class Reserva
{
    [FirestoreDocumentId] public string Id { get; set; } = "";
    [FirestoreProperty] public string SalaId { get; set; } = "";
    [FirestoreProperty] public string ProfessorId { get; set; } = "";
    [FirestoreProperty] public string TurmaId { get; set; } = "";
    [FirestoreProperty] public string CriadoPorId { get; set; } = "";
    [FirestoreProperty] public string Atividade { get; set; } = "";
    [FirestoreProperty] public NivelUso Nivel { get; set; }
    [FirestoreProperty] public Timestamp Inicio { get; set; }
    [FirestoreProperty] public Timestamp Fim { get; set; }
    [FirestoreProperty] public string Status { get; set; } = "Confirmada";
    [FirestoreProperty] public string CanceladoPorId { get; set; } = "";
    [FirestoreProperty] public string MotivoCancelamento { get; set; } = "";
    [FirestoreProperty] public Timestamp? CanceladoEm { get; set; }
}
