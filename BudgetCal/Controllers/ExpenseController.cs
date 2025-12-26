using Microsoft.AspNetCore.Mvc;
using BudgetCal.Models;

namespace BudgetCal.Controllers;

public class ExpenseController : Controller
{
    // In-memory storage for expenses (replace with database later)
    private static List<Expense> _expenses = new List<Expense>();
    private static int _nextId = 1;

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
    public IActionResult GetExpenses(int year, int month)
    {
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var expenses = _expenses
            .Where(e => e.Date >= startDate && e.Date <= endDate)
            .OrderBy(e => e.Date)
            .ToList();

        // Generate recurring expense instances for this month
        var recurringExpenses = GenerateRecurringExpenses(startDate, endDate);
        expenses.AddRange(recurringExpenses);

        return Json(expenses.OrderBy(e => e.Date));
    }

    private List<Expense> GenerateRecurringExpenses(DateTime startDate, DateTime endDate)
    {
        var generatedExpenses = new List<Expense>();
        var recurringExpenses = _expenses.Where(e => e.IsRecurring).ToList();

        foreach (var recurring in recurringExpenses)
        {
            var recurringStart = recurring.RecurringStartDate ?? recurring.Date;
            
            // Only generate if the recurring start is before or within the period
            if (recurringStart > endDate)
                continue;

            var currentDate = recurringStart;
            
            // Advance to first occurrence in the period
            while (currentDate < startDate)
            {
                currentDate = AddRecurringInterval(currentDate, recurring.RecurringInterval!.Value, recurring.RecurringPeriod!);
            }

            // Generate all occurrences within the period
            while (currentDate <= endDate)
            {
                // Don't duplicate the original expense
                if (currentDate.Date != recurring.Date.Date)
                {
                    generatedExpenses.Add(new Expense
                    {
                        Id = recurring.Id, // Use same ID to link to parent
                        Date = currentDate,
                        Amount = recurring.Amount,
                        Description = recurring.Description,
                        Category = recurring.Category,
                        IsRecurring = true,
                        RecurringInterval = recurring.RecurringInterval,
                        RecurringPeriod = recurring.RecurringPeriod,
                        RecurringStartDate = recurring.RecurringStartDate,
                        ParentRecurringExpenseId = recurring.Id
                    });
                }
                
                currentDate = AddRecurringInterval(currentDate, recurring.RecurringInterval!.Value, recurring.RecurringPeriod!);
            }
        }

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

    [HttpGet]
    public IActionResult GetDailyBalances(int year, int month)
    {
        var startDate = new DateTime(year, month, 1);
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var balances = new Dictionary<string, decimal>();

        for (int day = 1; day <= daysInMonth; day++)
        {
            var currentDate = new DateTime(year, month, day);
            var balance = CalculateBalanceForDate(currentDate);
            balances[currentDate.ToString("yyyy-MM-dd")] = balance;
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

        // Calculate total expenses from start date up to and including the current date
        var totalExpenses = _expenses
            .Where(e => e.Date >= startDate && e.Date.Date <= date.Date)
            .Sum(e => e.Amount);

        // Add recurring expenses up to this date
        var recurringExpenses = GenerateRecurringExpenses(startDate, date);
        var totalRecurring = recurringExpenses
            .Where(e => e.Date.Date <= date.Date)
            .Sum(e => e.Amount);

        return startBalance - totalExpenses - totalRecurring;
    }

    [HttpPost]
    public IActionResult Create([FromBody] Expense expense)
    {
        expense.Id = _nextId++;
        _expenses.Add(expense);
        return Json(expense);
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
            existing.IsRecurring = expense.IsRecurring;
            existing.RecurringInterval = expense.RecurringInterval;
            existing.RecurringPeriod = expense.RecurringPeriod;
            existing.RecurringStartDate = expense.RecurringStartDate;
            return Json(existing);
        }
        return NotFound();
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
}
