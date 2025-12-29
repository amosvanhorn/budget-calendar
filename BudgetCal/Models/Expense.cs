namespace BudgetCal.Models;

public enum RecurringEditMode
{
    ThisOne,      // Edit only this instance
    FromThisOne,  // Edit this and all future instances
    AllInSeries   // Edit all instances in the series
}

public enum TransactionType
{
    Debit,   // Decreases balance (expenses)
    Credit   // Increases balance (income)
}

public class Item
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Color { get; set; } = "#e3f2fd"; // Default light blue
    public TransactionType Type { get; set; } = TransactionType.Debit; // Default to debit (expense)

    // Recurring item properties
    public bool IsRecurring { get; set; }
    public int? RecurringInterval { get; set; }
    public string? RecurringPeriod { get; set; } // "days", "weeks", or "months"
    public DateTime? RecurringStartDate { get; set; }
    public DateTime? RecurringEndDate { get; set; } // When the recurring series ends

    // Helper property to identify if this is a generated recurring instance
    public int? ParentRecurringItemId { get; set; }

    // Tracks if this instance is a modified exception from the series
    public bool IsException { get; set; }
    public DateTime? OriginalDate { get; set; } // For exceptions, stores the original recurring date

    // Layer association
    public int? LayerId { get; set; }
}

// Keeping Expense as an alias for backward compatibility during migration
public class Expense : Item { }
