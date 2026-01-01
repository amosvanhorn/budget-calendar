using Microsoft.AspNetCore.Mvc;
using BudgetCal.Models;

namespace BudgetCal.Controllers;

public class ExpenseController : Controller
{
    // In-memory storage for items (data persisted to localStorage on client side)
    private static List<Account> _accounts = new List<Account> 
    { 
        new Account { Id = 1, Name = "Default Account", Description = "Your primary money account" } 
    };
    private static List<Item> _items = new List<Item>();
    private static List<Layer> _layers = new List<Layer>();
    private static int _nextId = 1;
    private static int _nextLayerId = 1;
    private static int _nextAccountId = 2;
    private static Dictionary<int, Dictionary<string, decimal>> _balanceOverrides = new Dictionary<int, Dictionary<string, decimal>>();
    private const string StorageKey = "budget_calendar_items";

    public IActionResult Index(int? year, int? month, int? accountId)
    {
        var now = DateTime.Now;
        var selectedYear = year ?? now.Year;
        var selectedMonth = month ?? now.Month;
        var selectedAccountId = accountId ?? _accounts.FirstOrDefault()?.Id ?? 1;

        ViewBag.Year = selectedYear;
        ViewBag.Month = selectedMonth;
        ViewBag.AccountId = selectedAccountId;
        ViewBag.MonthName = new DateTime(selectedYear, selectedMonth, 1).ToString("MMMM yyyy");
        ViewBag.Accounts = _accounts;

        return View(_items.Where(i => i.AccountId == selectedAccountId).ToList());
    }

    [HttpGet]
    public IActionResult GetAccounts()
    {
        return Json(_accounts);
    }

    [HttpPost]
    public IActionResult CreateAccount([FromBody] Account account)
    {
        account.Id = _nextAccountId++;
        _accounts.Add(account);
        return Json(new { success = true, account = account });
    }

    [HttpPut]
    public IActionResult UpdateAccount([FromBody] Account account)
    {
        var existing = _accounts.FirstOrDefault(a => a.Id == account.Id);
        if (existing != null)
        {
            existing.Name = account.Name;
            existing.Description = account.Description;
            existing.StartDate = account.StartDate;
            existing.StartingBalance = account.StartingBalance;
            return Json(new { success = true, account = existing });
        }
        return NotFound();
    }

    [HttpDelete]
    public IActionResult DeleteAccount(int id)
    {
        var account = _accounts.FirstOrDefault(a => a.Id == id);
        if (account != null)
        {
            _accounts.Remove(account);
            _items.RemoveAll(i => i.AccountId == id);
            _layers.RemoveAll(l => l.AccountId == id);
            _balanceOverrides.Remove(id);
            return Json(new { success = true });
        }
        return NotFound();
    }

    [HttpGet]
    public IActionResult GetAllExpenses(int accountId)
    {
        return Json(_items.Where(i => i.AccountId == accountId).ToList());
    }

    [HttpGet]
    public IActionResult GetExpenses(int year, int month, int accountId, bool defaultActive = true)
    {
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var activeLayerIds = _layers.Where(l => l.AccountId == accountId && l.IsActive).Select(l => (int?)l.Id).ToHashSet();

        // Get only non-recurring, non-exception items (one-time items)
        var items = _items
            .Where(e => e.AccountId == accountId && e.Date >= startDate && e.Date <= endDate && !e.IsRecurring && !e.IsException)
            .Where(e => (e.LayerId.HasValue && activeLayerIds.Contains(e.LayerId)) || (!e.LayerId.HasValue && defaultActive))
            .OrderBy(e => e.Date)
            .ToList();

        // Generate recurring item instances for this month
        var recurringItems = GenerateRecurringItems(startDate, endDate, accountId, defaultActive);
        items.AddRange(recurringItems);

        return Json(items.OrderBy(e => e.Date));
    }

    private List<Item> GenerateRecurringItems(DateTime startDate, DateTime endDate, int accountId, bool defaultActive = true)
    {
        var generatedItems = new List<Item>();
        var activeLayerIds = _layers.Where(l => l.AccountId == accountId && l.IsActive).Select(l => (int?)l.Id).ToHashSet();

        var recurringItems = _items
            .Where(e => e.AccountId == accountId && e.IsRecurring)
            .Where(e => (e.LayerId.HasValue && activeLayerIds.Contains(e.LayerId)) || (!e.LayerId.HasValue && defaultActive))
            .ToList();
        
        var exceptions = _items
            .Where(e => e.AccountId == accountId && e.IsException)
            .Where(e => (e.LayerId.HasValue && activeLayerIds.Contains(e.LayerId)) || (!e.LayerId.HasValue && defaultActive))
            .ToList();

        foreach (var recurring in recurringItems)
        {
            var recurringStart = recurring.RecurringStartDate ?? recurring.Date;
            var recurringEnd = recurring.RecurringEndDate;

            // Only generate if the recurring start is before or within the period
            if (recurringStart > endDate)
                continue;

            // Skip if the series ended before this period
            if (recurringEnd.HasValue && recurringEnd.Value < startDate)
                continue;

            // Add the parent recurring item if it falls within the date range
            // BUT only if:
            // 1. It hasn't ended before its own date
            // 2. There isn't an exception for this specific parent on this specific date
            var parentHasException = exceptions.Any(ex =>
                ex.ParentRecurringItemId == recurring.Id &&
                ex.OriginalDate.HasValue &&
                ex.OriginalDate.Value.Date == recurring.Date.Date);

            var parentIsExpired = recurring.RecurringEndDate.HasValue && recurring.RecurringEndDate.Value < recurring.Date;

            if (recurring.Date >= startDate && recurring.Date <= endDate && !parentHasException && !parentIsExpired)
            {
                generatedItems.Add(recurring);
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
                    ex.ParentRecurringItemId == recurring.Id &&
                    ex.OriginalDate.HasValue &&
                    ex.OriginalDate.Value.Date == currentDate.Date);

                // Don't duplicate the parent item or generate exceptions
                if (currentDate.Date != recurring.Date.Date && !hasException)
                {
                    generatedItems.Add(new Item
                    {
                        Id = recurring.Id, // Use same ID to link to parent
                        AccountId = recurring.AccountId,
                        Date = currentDate,
                        Amount = recurring.Amount,
                        Description = recurring.Description,
                        Color = recurring.Color,
                        Type = recurring.Type,
                        IsRecurring = true,
                        RecurringInterval = recurring.RecurringInterval,
                        RecurringPeriod = recurring.RecurringPeriod,
                        RecurringStartDate = recurring.RecurringStartDate,
                        RecurringEndDate = recurring.RecurringEndDate,
                        ParentRecurringItemId = recurring.Id,
                        LayerId = recurring.LayerId
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
        generatedItems.AddRange(validExceptions);

        return generatedItems;
    }

    private DateTime AddRecurringInterval(DateTime date, int interval, string period)
    {
        return period.ToLower() switch
        {
            "days" => date.AddDays(interval),
            "weeks" => date.AddDays(interval * 7),
            "months" => date.AddMonths(interval),
            "years" => date.AddYears(interval),
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
            "years" => date.AddYears(-interval),
            _ => date
        };
    }

    [HttpGet]
    public IActionResult GetDailyBalances(int year, int month, int accountId, bool defaultActive = true)
    {
        var startDate = new DateTime(year, month, 1);
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var balances = new Dictionary<string, object>();
        
        var accountOverrides = _balanceOverrides.ContainsKey(accountId) ? _balanceOverrides[accountId] : new Dictionary<string, decimal>();

        // Find the most recent override date before or in this month
        DateTime? lastOverrideDate = null;
        decimal? lastOverrideBalance = null;
        
        foreach (var kvp in accountOverrides.OrderBy(x => x.Key))
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
            if (accountOverrides.ContainsKey(dateStr))
            {
                balances[dateStr] = new { balance = accountOverrides[dateStr], isOverride = true };
                lastOverrideDate = currentDate;
                lastOverrideBalance = accountOverrides[dateStr];
            }
            else if (lastOverrideDate.HasValue && currentDate > lastOverrideDate.Value)
            {
                // Calculate from the last override date
                var balance = CalculateBalanceFromDate(lastOverrideDate.Value, lastOverrideBalance!.Value, currentDate, accountId, defaultActive);
                balances[dateStr] = new { balance = balance, isOverride = false };
            }
            else
            {
                // Use the original calculation
                var balance = CalculateBalanceForDate(currentDate, accountId, defaultActive);
                balances[dateStr] = new { balance = balance, isOverride = false };
            }
        }

        return Json(balances);
    }

    private decimal CalculateBalanceForDate(DateTime date, int accountId, bool defaultActive = true)
    {
        var account = _accounts.FirstOrDefault(a => a.Id == accountId);
        if (account == null) return 0;

        var startDate = account.StartDate;
        var startBalance = account.StartingBalance;

        // If the date is before the start date, return 0 or handle as needed
        if (date < startDate)
        {
            return 0;
        }

        var activeLayerIds = _layers.Where(l => l.AccountId == accountId && l.IsActive).Select(l => (int?)l.Id).ToHashSet();

        // Calculate total non-recurring items
        var totalDebits = _items
            .Where(e => e.AccountId == accountId && e.Date >= startDate && e.Date.Date <= date.Date && !e.IsRecurring && !e.IsException && e.Type == TransactionType.Debit)
            .Where(e => (e.LayerId.HasValue && activeLayerIds.Contains(e.LayerId)) || (!e.LayerId.HasValue && defaultActive))
            .Sum(e => e.Amount);

        var totalCredits = _items
            .Where(e => e.AccountId == accountId && e.Date >= startDate && e.Date.Date <= date.Date && !e.IsRecurring && !e.IsException && e.Type == TransactionType.Credit)
            .Where(e => (e.LayerId.HasValue && activeLayerIds.Contains(e.LayerId)) || (!e.LayerId.HasValue && defaultActive))
            .Sum(e => e.Amount);

        // Add recurring items up to this date (includes exceptions)
        var recurringItems = GenerateRecurringItems(startDate, date, accountId, defaultActive);
        var totalRecurringDebits = recurringItems
            .Where(e => e.Date.Date <= date.Date && e.Type == TransactionType.Debit)
            .Sum(e => e.Amount);

        var totalRecurringCredits = recurringItems
            .Where(e => e.Date.Date <= date.Date && e.Type == TransactionType.Credit)
            .Sum(e => e.Amount);

        return startBalance - totalDebits - totalRecurringDebits + totalCredits + totalRecurringCredits;
    }

    private decimal CalculateBalanceFromDate(DateTime fromDate, decimal fromBalance, DateTime toDate, int accountId, bool defaultActive = true)
    {
        var activeLayerIds = _layers.Where(l => l.AccountId == accountId && l.IsActive).Select(l => (int?)l.Id).ToHashSet();

        // Calculate items between fromDate (exclusive) and toDate (inclusive)
        var totalDebits = _items
            .Where(e => e.AccountId == accountId && e.Date.Date > fromDate.Date && e.Date.Date <= toDate.Date && !e.IsRecurring && !e.IsException && e.Type == TransactionType.Debit)
            .Where(e => (e.LayerId.HasValue && activeLayerIds.Contains(e.LayerId)) || (!e.LayerId.HasValue && defaultActive))
            .Sum(e => e.Amount);

        var totalCredits = _items
            .Where(e => e.AccountId == accountId && e.Date.Date > fromDate.Date && e.Date.Date <= toDate.Date && !e.IsRecurring && !e.IsException && e.Type == TransactionType.Credit)
            .Where(e => (e.LayerId.HasValue && activeLayerIds.Contains(e.LayerId)) || (!e.LayerId.HasValue && defaultActive))
            .Sum(e => e.Amount);

        // Add recurring items in this range
        var recurringItems = GenerateRecurringItems(fromDate.AddDays(1), toDate, accountId, defaultActive);
        var totalRecurringDebits = recurringItems
            .Where(e => e.Date.Date > fromDate.Date && e.Date.Date <= toDate.Date && e.Type == TransactionType.Debit)
            .Sum(e => e.Amount);

        var totalRecurringCredits = recurringItems
            .Where(e => e.Date.Date > fromDate.Date && e.Date.Date <= toDate.Date && e.Type == TransactionType.Credit)
            .Sum(e => e.Amount);

        return fromBalance - totalDebits - totalRecurringDebits + totalCredits + totalRecurringCredits;
    }

    [HttpPost]
    public IActionResult Create([FromBody] Item item)
    {
        item.Id = _nextId++;
        _items.Add(item);
        return Json(new { success = true, expense = item });
    }

    [HttpGet]
    public IActionResult GetLayers()
    {
        return Json(_layers);
    }

    [HttpPost]
    public IActionResult CreateLayer([FromBody] Layer layer)
    {
        layer.Id = _nextLayerId++;
        _layers.Add(layer);
        return Json(new { success = true, layer = layer });
    }

    [HttpPut]
    public IActionResult UpdateLayer([FromBody] Layer layer)
    {
        var existing = _layers.FirstOrDefault(l => l.Id == layer.Id && l.AccountId == layer.AccountId);
        if (existing != null)
        {
            existing.Name = layer.Name;
            return Json(new { success = true, layer = existing });
        }
        return NotFound();
    }

    [HttpPost]
    public IActionResult ToggleLayer(int id, int accountId)
    {
        var layer = _layers.FirstOrDefault(l => l.Id == id && l.AccountId == accountId);
        if (layer != null)
        {
            layer.IsActive = !layer.IsActive;
            return Json(new { success = true, layer = layer });
        }
        return NotFound();
    }

    [HttpDelete]
    public IActionResult DeleteLayer(int id, int accountId)
    {
        var layer = _layers.FirstOrDefault(l => l.Id == id && l.AccountId == accountId);
        if (layer != null)
        {
            _layers.Remove(layer);
            _items.RemoveAll(i => i.LayerId == id && i.AccountId == accountId);
            return Json(new { success = true });
        }
        return NotFound();
    }

    [HttpPost]
    public IActionResult LoadFromStorage([FromBody] LoadStorageRequest request)
    {
        _accounts = request.Accounts ?? new List<Account> { new Account { Id = 1, Name = "Default Account", Description = "Your primary money account" } };
        _items = request.Items ?? new List<Item>();
        _layers = request.Layers ?? new List<Layer>();
        _balanceOverrides = request.BalanceOverrides ?? new Dictionary<int, Dictionary<string, decimal>>();
        
        if (_accounts.Any())
        {
            _nextAccountId = _accounts.Max(a => a.Id) + 1;
        }

        if (_items.Any())
        {
            _nextId = _items.Max(e => e.Id) + 1;
        }
        else
        {
            _nextId = 1;
        }

        if (_layers.Any())
        {
            _nextLayerId = _layers.Max(l => l.Id) + 1;
        }
        else
        {
            _nextLayerId = 1;
        }

        return Json(new { success = true });
    }

    [HttpGet]
    public IActionResult GetAllData()
    {
        return Json(new { accounts = _accounts, expenses = _items, layers = _layers, balanceOverrides = _balanceOverrides });
    }

    [HttpPost]
    public IActionResult SetBalanceOverride([FromBody] BalanceOverrideRequest request)
    {
        if (!_balanceOverrides.ContainsKey(request.AccountId))
        {
            _balanceOverrides[request.AccountId] = new Dictionary<string, decimal>();
        }
        _balanceOverrides[request.AccountId][request.Date] = request.Balance;
        return Json(new { success = true });
    }

    [HttpPost]
    public IActionResult ClearAll()
    {
        _accounts = new List<Account> { new Account { Id = 1, Name = "Default Account", Description = "Your primary money account" } };
        _items.Clear();
        _layers.Clear();
        _balanceOverrides.Clear();
        _nextId = 1;
        _nextLayerId = 1;
        _nextAccountId = 2;
        return Json(new { success = true });
    }

    public class LoadStorageRequest
    {
        public List<Account>? Accounts { get; set; }
        public List<Item>? Items { get; set; }
        public List<Layer>? Layers { get; set; }
        public Dictionary<int, Dictionary<string, decimal>>? BalanceOverrides { get; set; }
    }

    public class BalanceOverrideRequest
    {
        public int AccountId { get; set; }
        public string Date { get; set; } = string.Empty;
        public decimal Balance { get; set; }
    }

    [HttpPut]
    public IActionResult Update([FromBody] Item item)
    {
        var existing = _items.FirstOrDefault(e => e.Id == item.Id && e.AccountId == item.AccountId);
        if (existing != null)
        {
            existing.Date = item.Date;
            existing.Amount = item.Amount;
            existing.Description = item.Description;
            existing.Color = item.Color;
            existing.Type = item.Type;
            existing.IsRecurring = item.IsRecurring;
            existing.RecurringInterval = item.RecurringInterval;
            existing.RecurringPeriod = item.RecurringPeriod;
            existing.RecurringStartDate = item.RecurringStartDate;
            existing.LayerId = item.LayerId;
            return Json(existing);
        }
        return NotFound();
    }

    [HttpPut]
    public IActionResult UpdateRecurring([FromBody] Item item, [FromQuery] string mode)
    {
        var parentItem = _items.FirstOrDefault(e => e.Id == item.Id && e.AccountId == item.AccountId);
        if (parentItem == null) return NotFound();

        var editMode = Enum.Parse<RecurringEditMode>(mode);

        switch (editMode)
        {
            case RecurringEditMode.ThisOne:
                // Check if we are editing the "parent" item of the series
                // (the one that actually holds the series definition)
                // Use .Date to ensure time components don't interfere
                if (parentItem.Date.Date == item.Date.Date)
                {
                    // Move the series start to the next occurrence
                    var nextDate = AddRecurringInterval(parentItem.Date.Date, 
                        parentItem.RecurringInterval!.Value, 
                        parentItem.RecurringPeriod!);
                    
                    parentItem.Date = nextDate;
                    parentItem.RecurringStartDate = nextDate;
                }

                // Create a new item as an exception for this single instance
                var exception = new Item
                {
                    Id = _nextId++,
                    AccountId = item.AccountId,
                    Date = item.Date.Date,
                    Amount = item.Amount,
                    Description = item.Description,
                    Color = item.Color,
                    Type = item.Type,
                    IsRecurring = false,
                    IsException = true,
                    OriginalDate = item.Date.Date,
                    ParentRecurringItemId = parentItem.Id,
                    LayerId = item.LayerId
                };
                _items.Add(exception);
                return Json(exception);

            case RecurringEditMode.FromThisOne:
                // If we are starting from the very first item, just update the whole series
                if (parentItem.Date.Date == item.Date.Date)
                {
                    parentItem.Amount = item.Amount;
                    parentItem.Description = item.Description;
                    parentItem.Color = item.Color;
                    parentItem.Type = item.Type;
                    parentItem.RecurringInterval = item.RecurringInterval;
                    parentItem.RecurringPeriod = item.RecurringPeriod;
                    parentItem.LayerId = item.LayerId;
                    return Json(parentItem);
                }

                // End the original series one interval before this date
                var editDate = item.Date.Date;
                var previousDate = SubtractRecurringInterval(editDate,
                    parentItem.RecurringInterval!.Value,
                    parentItem.RecurringPeriod!);
                parentItem.RecurringEndDate = previousDate;

                // Create a new recurring series starting from this date
                var newSeries = new Item
                {
                    Id = _nextId++,
                    AccountId = item.AccountId,
                    Date = editDate,
                    Amount = item.Amount,
                    Description = item.Description,
                    Color = item.Color,
                    Type = item.Type,
                    IsRecurring = true,
                    RecurringInterval = item.RecurringInterval,
                    RecurringPeriod = item.RecurringPeriod,
                    RecurringStartDate = editDate,
                    RecurringEndDate = null,
                    LayerId = item.LayerId
                };
                _items.Add(newSeries);
                return Json(newSeries);

            case RecurringEditMode.AllInSeries:
                // Update the parent item entirely
                parentItem.Amount = item.Amount;
                parentItem.Description = item.Description;
                parentItem.Color = item.Color;
                parentItem.Type = item.Type;
                parentItem.RecurringInterval = item.RecurringInterval;
                parentItem.RecurringPeriod = item.RecurringPeriod;
                parentItem.LayerId = item.LayerId;
                // Keep the original start date
                return Json(parentItem);

            default:
                return BadRequest("Invalid edit mode");
        }
    }

    [HttpDelete]
    public IActionResult Delete(int id, int accountId)
    {
        var item = _items.FirstOrDefault(e => e.Id == id && e.AccountId == accountId);
        if (item != null)
        {
            _items.Remove(item);
            return Ok();
        }
        return NotFound();
    }

    [HttpDelete]
    public IActionResult DeleteRecurring(int id, int accountId, string mode, DateTime date)
    {
        var parentItem = _items.FirstOrDefault(e => e.Id == id && e.AccountId == accountId);
        if (parentItem == null) return NotFound();

        var deleteMode = Enum.Parse<RecurringEditMode>(mode);

        switch (deleteMode)
        {
            case RecurringEditMode.ThisOne:
                // If we are deleting the first item in the series
                if (parentItem.Date.Date == date.Date)
                {
                    // Move the series start to the next occurrence
                    var nextDate = AddRecurringInterval(parentItem.Date.Date,
                        parentItem.RecurringInterval!.Value,
                        parentItem.RecurringPeriod!);

                    parentItem.Date = nextDate;
                    parentItem.RecurringStartDate = nextDate;
                    return Ok();
                }

                // Create an exception marker for this date (so it won't generate)
                var exception = new Item
                {
                    Id = _nextId++,
                    AccountId = accountId,
                    Date = date.Date,
                    Amount = 0,
                    Description = "[DELETED]",
                    IsRecurring = false,
                    IsException = true,
                    OriginalDate = date.Date,
                    ParentRecurringItemId = parentItem.Id,
                    LayerId = parentItem.LayerId
                };
                _items.Add(exception);
                return Ok();

            case RecurringEditMode.FromThisOne:
                // If we are deleting from the very first item, just delete the whole series
                if (parentItem.Date.Date == date.Date)
                {
                    _items.Remove(parentItem);
                    _items.RemoveAll(e => e.ParentRecurringItemId == id && e.AccountId == accountId);
                    return Ok();
                }

                // End the original series one interval before this date
                var deleteDate = date.Date;
                var previousDate = SubtractRecurringInterval(deleteDate,
                    parentItem.RecurringInterval!.Value,
                    parentItem.RecurringPeriod!);
                parentItem.RecurringEndDate = previousDate;
                return Ok();

            case RecurringEditMode.AllInSeries:
                // Delete the entire series
                _items.Remove(parentItem);
                // Also remove any exceptions
                _items.RemoveAll(e => e.ParentRecurringItemId == id && e.AccountId == accountId);
                return Ok();

            default:
                return BadRequest("Invalid delete mode");
        }
    }
}
