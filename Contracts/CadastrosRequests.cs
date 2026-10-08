using System.ComponentModel.DataAnnotations;

namespace Salas_Elvis.Contracts;

public class LoginRequest
{
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
    [Required, StringLength(100)] public string Senha { get; set; } = "";
}

public class CriarSalaRequest
{
    [Required, StringLength(100)] public string Nome { get; set; } = "";
    [Range(1, 1000)] public int Capacidade { get; set; }
    [Required, RegularExpression("^(Sala de aula|Informática|Laboratório|Oficina|Auditório|Outro)$")]
    public string Tipo { get; set; } = "Sala de aula";
    [StringLength(150)] public string Localizacao { get; set; } = "";
    public bool AltoDesempenho { get; set; }
}

public class CriarTurmaRequest
{
    [Required, StringLength(100)] public string Nome { get; set; } = "";
    [Required, StringLength(150)] public string Curso { get; set; } = "";
    [Required, RegularExpression("^(Manhã|Tarde|Noite|Integral)$")]
    public string Turno { get; set; } = "Manhã";
    [Range(1, 1000)] public int QuantidadeAlunos { get; set; }
}

public class CancelarReservaRequest
{
    [Required, StringLength(500, MinimumLength = 3)]
    public string Motivo { get; set; } = "Cancelada pela coordenação.";
}
