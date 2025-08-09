using System.ComponentModel.DataAnnotations;

namespace GPInventory.Application.DTOs.Expenses;

public class CreateExpenseCategoryDto
{
    [Required]
    [StringLength(200, ErrorMessage = "El nombre no puede exceder 200 caracteres")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "La descripción no puede exceder 500 caracteres")]
    public string? Description { get; set; }

    public int? BusinessId { get; set; }
}

public class UpdateExpenseCategoryDto
{
    [StringLength(200, ErrorMessage = "El nombre no puede exceder 200 caracteres")]
    public string? Name { get; set; }

    [StringLength(500, ErrorMessage = "La descripción no puede exceder 500 caracteres")]
    public string? Description { get; set; }
}