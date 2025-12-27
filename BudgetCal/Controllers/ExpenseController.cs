using Microsoft.AspNetCore.Mvc;
using BudgetCal.Models;

namespace BudgetCal.Controllers;

public class ExpenseController : Controller
{
    // In-memory storage for expenses (data persisted to localStorage on client side)
    private static List<Expense> _expenses = new List<Expense>();
    private static int _nextId = 1;
    private static Dictionary<string, decimal> _balanceOverrides = new Dictionary<string, decimal>();
    private const string StorageKey = "budget_calendar_expenses";

    public IActionResult Index(int? year, int? month)
    {
        var now = DateTime.Now;
        var selectedYear = year ?? now.Year;
        var selectedMonth = month ?? now.Month;

        ViewBag.Year = selectedYear;
        ViewBag.Month = selectedMonth;
        ViewBag.MonthName = new DateTime(selectedYear, selectedMonth, 1).ToString("MMMM yyyy");

        return View(_expenses);
    }

    [HttpGet]
    public IActionResult GetAllExpenses()
    {
        return Json(_expenses);
    }

    [HttpGet]
    public IActionResult GetExpenses(int year, int month)
    {
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        // Get only non-recurring, non-exception expenses (one-time expenses)
        var expenses = _expenses
            .Where(e => e.Date >= startDate && e.Date <= endDate && !e.IsRecurring && !e.IsException)
            .OrderBy(e => e.Date)
            .ToList();

        // Generate recurring expense instances for this month (includes exceptions and parent recurring expenses in date range)
        var recurringExpenses = GenerateRecurringExpenses(startDate, endDate);
        expenses.AddRange(recurringExpenses);

        return Json(expenses.OrderBy(e => e.Date));
    }

    private List<Expense> GenerateRecurringExpenses(DateTime startDate, DateTime endDate)
    {
        var generatedExpenses = new List<Expense>();
        var recurringExpenses = _expenses.Where(e => e.IsRecurring).ToList();
        var exceptions = _expenses.Where(e => e.IsException).ToList();

        foreach (var recurring in recurringExpenses)
        {
            var recurringStart = recurring.RecurringStartDate ?? recurring.Date;
            var recurringEnd = recurring.RecurringEndDate;
            
            // Only generate if the recurring start is before or within the period
            if (recurringStart > endDate)
                continue;
                
            // Skip if the series ended before this period
            if (recurringEnd.HasValue && recurringEnd.Value < startDate)
                continue;

            // Add the parent recurring expense if it falls within the date range
            if (recurring.Date >= startDate && recurring.Date <= endDate)
            {
                generatedExpenses.Add(recurring);
            }

            var currentDate = recurringStart;
            
            // Advance to first occurrence in the period
            while (currentDate < startDate)
            {
                currentDate = AddRecurringInterval(currentDate, recurring.RecurringInterval!.Value, recurring.RecurringPeriod!);
            }

            // Generate all occurrences within the period
            while (currentDate <= endDate)
            {
                // Stop if we've passed the end date of this series
                if (recurringEnd.HasValue && currentDate > recurringEnd.Value)
                    break;
                    
                // Check if this date has an exception
                var hasException = exceptions.Any(ex => 
                    ex.ParentRecurringExpenseId == recurring.Id && 
                    ex.OriginalDate.HasValue &&
                    ex.OriginalDate.Value.Date == currentDate.Date);
                
                // Don't duplicate the parent expense or generate exceptions
                if (currentDate.Date != recurring.Date.Date && !hasException)
                {
                    generatedExpenses.Add(new Expense
                    {
                        Id = recurring.Id, // Use same ID to link to parent
                        Date = currentDate,
                        Amount = recurring.Amount,
                        Description = recurring.Description,
                        Category = recurring.Category,
                        Color = recurring.Color,
                        IsRecurring = true,
                        RecurringInterval = recurring.RecurringInterval,
                        RecurringPeriod = recurring.RecurringPeriod,
                        RecurringStartDate = recurring.RecurringStartDate,
                        RecurringEndDate = recurring.RecurringEndDate,
                        ParentRecurringExpenseId = recurring.Id
                    });
                }
                
                currentDate = AddRecurringInterval(currentDate, recurring.RecurringInterval!.Value, recurring.RecurringPeriod!);
            }
        }
        
        // Add non-deleted exceptions
        var validExceptions = exceptions
            .Where(e => e.Description != "[DELETED]" && 
                        e.Date >= startDate && 
                        e.Date <= endDate)
            .ToList();
        generatedExpenses.AddRange(validExceptions);

        return generatedExpenses;
    }

    private DateTime AddRecurringInterval(DateTime date, int interval, string period)
    {
        return period.ToLower() switch
        {
            "days" => date.AddDays(interval),
            "weeks" => date.AddDays(interval * 7),
            "months" => date.AddMonths(interval),
            _ => date
        };
    }

    private DateTime SubtractRecurringInterval(DateTime date, int interval, string period)
    {
        return period.ToLower() switch
        {
            "days" => date.AddDays(-interval),
            "weeks" => date.AddDays(-interval * 7),
            "months" => date.AddMonths(-interval),
            _ => date
        };
    }

    [HttpGet]
    public IActionResult GetDailyBalances(int year, int month)
    {
        var startDate = new DateTime(year, month, 1);
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var balances = new Dictionary<string, object>();
        
        // Find the most recent override date before or in this month
        DateTime? lastOverrideDate = null;
        decimal? lastOverrideBalance = null;
        
        foreach (var kvp in _balanceOverrides.OrderBy(x => x.Key))
        {
            var overrideDate = DateTime.Parse(kvp.Key);
            if (overrideDate <= startDate.AddMonths(1).AddDays(-1))
            {
                lastOverrideDate = overrideDate;
                lastOverrideBalance = kvp.Value;
            }
        }

        for (int day = 1; day <= daysInMonth; day++)
        {
            var currentDate = new DateTime(year, month, day);
            var dateStr = currentDate.ToString("yyyy-MM-dd");
            
            // Check if this specific date has an override
            if (_balanceOverrides.ContainsKey(dateStr))
            {
                balances[dateStr] = new { balance = _balanceOverrides[dateStr], isOverride = true };
                lastOverrideDate = currentDate;
                lastOverrideBalance = _balanceOverrides[dateStr];
            }
            else if (lastOverrideDate.HasValue && currentDate > lastOverrideDate.Value)
            {
                // Calculate from the last override date
                var balance = CalculateBalanceFromDate(lastOverrideDate.Value, lastOverrideBalance!.Value, currentDate);
                balances[dateStr] = new { balance = balance, isOverride = false };
            }
            else
            {
                // Use the original calculation
                var balance = CalculateBalanceForDate(currentDate);
                balances[dateStr] = new { balance = balance, isOverride = false };
            }
        }

        return Json(balances);
    }

    private decimal CalculateBalanceForDate(DateTime date)
    {
        var startDate = AccountBalance.StartDate;
        var startBalance = AccountBalance.StartingBalance;

        // If the date is before the start date, return 0 or handle as needed
        if (date < startDate)
        {
            return 0;
        }

        // Calculate total non-recurring expenses (excluding exceptions as they're handled separately)
        var totalExpenses = _expenses
            .Where(e => e.Date >= startDate && e.Date.Date <= date.Date && !e.IsRecurring && !e.IsException)
            .Sum(e => e.Amount);

        // Add recurring expenses up to this date (includes exceptions)
        var recurringExpenses = GenerateRecurringExpenses(startDate, date);
        var totalRecurring = recurringExpenses
            .Where(e => e.Date.Date <= date.Date)
            .Sum(e => e.Amount);

        return startBalance - totalExpenses - totalRecurring;
    }

    private decimal CalculateBalanceFromDate(DateTime fromDate, decimal fromBalance, DateTime toDate)
    {
        // Calculate expenses between fromDate (exclusive) and toDate (inclusive)
        var totalExpenses = _expenses
            .Where(e => e.Date.Date > fromDate.Date && e.Date.Date <= toDate.Date && !e.IsRecurring && !e.IsException)
            .Sum(e => e.Amount);

        // Add recurring expenses in this range
        var recurringExpenses = GenerateRecurringExpenses(fromDate.AddDays(1), toDate);
        var totalRecurring = recurringExpenses
            .Where(e => e.Date.Date > fromDate.Date && e.Date.Date <= toDate.Date)
            .Sum(e => e.Amount);

        return fromBalance - totalExpenses - totalRecurring;
    }

    [HttpPost]
    public IActionResult Create([FromBody] Expense expense)
    {
        expense.Id = _nextId++;
        _expenses.Add(expense);
        return Json(new { success = true, expense = expense });
    }
    
    [HttpPost]
    public IActionResult LoadFromStorage([FromBody] LoadStorageRequest request)
    {
        _expenses = request.Expenses ?? new List<Expense>();
        _balanceOverrides = request.BalanceOverrides ?? new Dictionary<string, decimal>();
        if (_expenses.Any())
        {
            _nextId = _expenses.Max(e => e.Id) + 1;
        }
        else
        {
            _nextId = 1;
        }
        return Json(new { success = true });
    }
    
    [HttpGet]
    public IActionResult GetAllData()
    {
        return Json(new { expenses = _expenses, balanceOverrides = _balanceOverrides });
    }
    
    [HttpPost]
    public IActionResult SetBalanceOverride([FromBody] BalanceOverrideRequest request)
    {
        _balanceOverrides[request.Date] = request.Balance;
        return Json(new { success = true });
    }
    
    [HttpPost]
    public IActionResult ClearAll()
    {
        _expenses.Clear();
        _balanceOverrides.Clear();
        _nextId = 1;
        return Json(new { success = true });
    }

    public class LoadStorageRequest
    {
        public List<Expense> Expenses { get; set; } = new List<Expense>();
        public Dictionary<string, decimal> BalanceOverrides { get; set; } = new Dictionary<string, decimal>();
    }

    public class BalanceOverrideRequest
    {
        public string Date { get; set; } = string.Empty;
        public decimal Balance { get; set; }
    }

    [HttpPut]
    public IActionResult Update([FromBody] Expense expense)
    {
        var existing = _expenses.FirstOrDefault(e => e.Id == expense.Id);
        if (existing != null)
        {
            existing.Date = expense.Date;
            existing.Amount = expense.Amount;
            existing.Description = expense.Description;
            existing.Category = expense.Category;
            existing.Color = expense.Color;
            existing.IsRecurring = expense.IsRecurring;
            existing.RecurringInterval = expense.RecurringInterval;
            existing.RecurringPeriod = expense.RecurringPeriod;
            existing.RecurringStartDate = expense.RecurringStartDate;
            return Json(existing);
        }
        return NotFound();
    }

    [HttpPut]
    public IActionResult UpdateRecurring([FromBody] Expense expense, [FromQuery] string mode)
    {
        var parentExpense = _expenses.FirstOrDefault(e => e.Id == expense.Id);
        if (parentExpense == null) return NotFound();

        var editMode = Enum.Parse<RecurringEditMode>(mode);
        
        switch (editMode)
        {
            case RecurringEditMode.ThisOne:
                // Create a new expense as an exception for this single instance
                var exception = new Expense
                {
                    Id = _nextId++,
                    Date = expense.Date,
                    Amount = expense.Amount,
                    Description = expense.Description,
                    Category = expense.Category,
                    Color = expense.Color,
                    IsRecurring = false,
                    IsException = true,
                    OriginalDate = expense.Date,
                    ParentRecurringExpenseId = parentExpense.Id
                };
                _expenses.Add(exception);
                return Json(exception);
                
            case RecurringEditMode.FromThisOne:
                // End the original series one interval before this date
                var editDate = expense.Date;
                var previousDate = SubtractRecurringInterval(editDate, 
                    parentExpense.RecurringInterval!.Value, 
                    parentExpense.RecurringPeriod!);
                parentExpense.RecurringEndDate = previousDate;
                
                // Create a new recurring series starting from this date
                var newSeries = new Expense
                {
                    Id = _nextId++,
                    Date = editDate,
                    Amount = expense.Amount,
                    Description = expense.Description,
                    Category = expense.Category,
                    Color = expense.Color,
                    IsRecurring = true,
                    RecurringInterval = expense.RecurringInterval,
                    RecurringPeriod = expense.RecurringPeriod,
                    RecurringStartDate = editDate,
                    RecurringEndDate = null
                };
                _expenses.Add(newSeries);
                return Json(newSeries);
                
            case RecurringEditMode.AllInSeries:
                // Update the parent expense entirely
                parentExpense.Amount = expense.Amount;
                parentExpense.Description = expense.Description;
                parentExpense.Category = expense.Category;
                parentExpense.Color = expense.Color;
                parentExpense.RecurringInterval = expense.RecurringInterval;
                parentExpense.RecurringPeriod = expense.RecurringPeriod;
                // Keep the original start date
                return Json(parentExpense);
                
            default:
                return BadRequest("Invalid edit mode");
        }
    }

    [HttpDelete]
    public IActionResult Delete(int id)
    {
        var expense = _expenses.FirstOrDefault(e => e.Id == id);
        if (expense != null)
        {
            _expenses.Remove(expense);
            return Ok();
        }
        return NotFound();
    }

    [HttpDelete]
    public IActionResult DeleteRecurring(int id, string mode, DateTime date)
    {
        var parentExpense = _expenses.FirstOrDefault(e => e.Id == id);
        if (parentExpense == null) return NotFound();

        var deleteMode = Enum.Parse<RecurringEditMode>(mode);
        
        switch (deleteMode)
        {
            case RecurringEditMode.ThisOne:
                // Create an exception marker for this date (so it won't generate)
                var exception = new Expense
                {
                    Id = _nextId++,
                    Date = date,
                    Amount = 0,
                    Description = "[DELETED]",
                    Category = "System",
                    IsRecurring = false,
                    IsException = true,
                    OriginalDate = date,
                    ParentRecurringExpenseId = parentExpense.Id
                };
                _expenses.Add(exception);
                return Ok();
                
            case RecurringEditMode.FromThisOne:
                // End the original series one interval before this date
                var deleteDate = date;
                var previousDate = SubtractRecurringInterval(deleteDate, 
                    parentExpense.RecurringInterval!.Value, 
                    parentExpense.RecurringPeriod!);
                parentExpense.RecurringEndDate = previousDate;
                return Ok();
                
            case RecurringEditMode.AllInSeries:
                // Delete the entire series
                _expenses.Remove(parentExpense);
                // Also remove any exceptions
                _expenses.RemoveAll(e => e.ParentRecurringExpenseId == id);
                return Ok();
                
            default:
                return BadRequest("Invalid delete mode");
        }
    }
}
