using System.ComponentModel.DataAnnotations;

namespace GPInventory.Application.DTOs.Prospects;

public class CreateProspectDto
{
    [Required(ErrorMessage = "El nombre es requerido")]
    [StringLength(255, ErrorMessage = "El nombre no puede exceder 255 caracteres")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "El email es requerido")]
    [EmailAddress(ErrorMessage = "El formato del email no es válido")]
    [StringLength(255, ErrorMessage = "El email no puede exceder 255 caracteres")]
    public string Mail { get; set; } = string.Empty;

    [Required(ErrorMessage = "El contacto es requerido")]
    [StringLength(50, ErrorMessage = "El contacto no puede exceder 50 caracteres")]
    public string Contact { get; set; } = string.Empty;

    [StringLength(255, ErrorMessage = "El nombre de la empresa no puede exceder 255 caracteres")]
    public string? Enterprise { get; set; }

    [StringLength(255, ErrorMessage = "El asunto no puede exceder 255 caracteres")]
    public string? Subject { get; set; }

    [Required(ErrorMessage = "La descripción es requerida")]
    [StringLength(2000, ErrorMessage = "La descripción no puede exceder 2000 caracteres")]
    public string Description { get; set; } = string.Empty;
}
