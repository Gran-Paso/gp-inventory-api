namespace GPInventory.Application.DTOs.Production;

public class UnitMeasureDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Symbol { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsActive { get; set; }
}

public class CreateUnitMeasureDto
{
    public string Name { get; set; } = string.Empty;
    public string? Symbol { get; set; }
    public string? Description { get; set; }
}

public class UpdateUnitMeasureDto
{
    public string Name { get; set; } = string.Empty;
    public string? Symbol { get; set; }
    public string? Description { get; set; }
}
