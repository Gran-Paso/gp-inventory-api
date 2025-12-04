namespace GPInventory.Application.DTOs.Payments;

public class ReceiptTypeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class PaymentTypeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class PaymentMethodDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class BankEntityDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class CreateBankEntityDto
{
    public string Name { get; set; } = string.Empty;
}
