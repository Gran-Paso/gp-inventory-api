namespace GPInventory.Domain.Entities;

public class Prospect
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Mail { get; set; } = string.Empty;
    public string Contact { get; set; } = string.Empty;
    public string? Enterprise { get; set; }
    public string? Subject { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
