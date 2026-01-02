namespace BudgetCal.Models;

public class Account
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartDate { get; set; } = new DateTime(2025, 9, 1);
    public decimal StartingBalance { get; set; } = 1000m;
}
