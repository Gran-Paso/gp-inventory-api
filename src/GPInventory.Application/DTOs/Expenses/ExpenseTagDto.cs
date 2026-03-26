using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace GPInventory.Application.DTOs.Expenses;

public class ExpenseTagDto
{
    public int Id { get; set; }

    [JsonPropertyName("business_id")]
    public int BusinessId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Color hex, ej "#ef4444"</summary>
    public string Color { get; set; } = "#6b7280";

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}

public class CreateExpenseTagDto
{
    [Required]
    [JsonPropertyName("business_id")]
    public int BusinessId { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(7)]
    public string Color { get; set; } = "#6b7280";
}

public class UpdateExpenseTagDto
{
    [StringLength(100)]
    public string? Name { get; set; }

    [StringLength(7)]
    public string? Color { get; set; }
}

public class AssignTagsDto
{
    /// <summary>IDs de las etiquetas a asignar (reemplaza las existentes)</summary>
    [JsonPropertyName("tag_ids")]
    public List<int> TagIds { get; set; } = new();
}
