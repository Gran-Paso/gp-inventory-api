using System.ComponentModel.DataAnnotations;

namespace GPInventory.Domain.Entities;

public class TimeUnit : BaseEntity
{
    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(200)]
    public string? Description { get; set; }

    // Collection navigation properties
    public ICollection<Process> Processes { get; set; } = new List<Process>();

    public TimeUnit()
    {
    }

    public TimeUnit(string name, string? description = null)
    {
        Name = name;
        Description = description;
    }
}
