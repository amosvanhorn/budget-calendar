namespace BudgetCal.Models;

public enum RecurringEditMode
{
    ThisOne,      // Edit only this instance
    FromThisOne,  // Edit this and all future instances
    AllInSeries   // Edit all instances in the series
}

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
    public DateTime? RecurringEndDate { get; set; } // When the recurring series ends
    
    // Helper property to identify if this is a generated recurring instance
    public int? ParentRecurringExpenseId { get; set; }
    
    // Tracks if this instance is a modified exception from the series
    public bool IsException { get; set; }
    public DateTime? OriginalDate { get; set; } // For exceptions, stores the original recurring date
}
