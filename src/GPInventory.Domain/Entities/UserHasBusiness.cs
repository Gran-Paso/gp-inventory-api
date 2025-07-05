namespace GPInventory.Domain.Entities;

public class UserHasBusiness : BaseEntity
{
    public int BusinessId { get; set; }
    public int RoleId { get; set; }
    public int UserId { get; set; }

    // Navigation properties
    public virtual Business Business { get; set; } = null!;
    public virtual Role Role { get; set; } = null!;
    public virtual User User { get; set; } = null!;

    public UserHasBusiness()
    {
    }

    public UserHasBusiness(int userId, int businessId, int roleId)
    {
        UserId = userId;
        BusinessId = businessId;
        RoleId = roleId;
    }
}
