using System.ComponentModel.DataAnnotations;
using Salas_Elvis.Models;

namespace Salas_Elvis.Contracts;

public class CriarReservaRequest : IValidatableObject
{
    [Required, RegularExpression(@"^[^/]+$")]
    public string SalaId { get; set; } = "";
    [RegularExpression(@"^[^/]+$")]
    public string? ProfessorId { get; set; }
    [Required, RegularExpression(@"^[^/]+$")]
    public string TurmaId { get; set; } = "";
    [Required, StringLength(500)]
    public string Atividade { get; set; } = "";
    [Required, EnumDataType(typeof(NivelUso))]
    public NivelUso? Nivel { get; set; }
    [Required] public DateTimeOffset? Inicio { get; set; }
    [Required] public DateTimeOffset? Fim { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Inicio.HasValue && Fim.HasValue && Fim <= Inicio)
            yield return new ValidationResult("O fim deve ser posterior ao início.", [nameof(Fim)]);
    }
}
