namespace BudgetCal.Models;

public class Expense
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    
    // Recurring expense properties
    public bool IsRecurring { get; set; }
    public int? RecurringInterval { get; set; }
    public string? RecurringPeriod { get; set; } // "days", "weeks", or "months"
    public DateTime? RecurringStartDate { get; set; }
    
    // Helper property to identify if this is a generated recurring instance
    public int? ParentRecurringExpenseId { get; set; }
}
