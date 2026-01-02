using BudgetCal.Controllers;
using BudgetCal.Models;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace BudgetCal.Tests;

[Collection("ExpenseController Tests")]
public class ExpenseControllerTests
{
    private ExpenseController CreateController()
    {
        var controller = new ExpenseController();
        controller.ClearAll(); // Reset static state before each test
        return controller;
    }

    [Fact]
    public void GetAccounts_ReturnsOkResult_WithListOfAccounts()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = controller.GetAccounts();

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
        var accounts = Assert.IsAssignableFrom<IEnumerable<Account>>(jsonResult.Value);
        Assert.NotEmpty(accounts);
    }

    [Fact]
    public void CreateAccount_AddsAccount_AndReturnsSuccess()
    {
        // Arrange
        var controller = CreateController();
        var newAccount = new Account { Name = "Test Account", Description = "Test Description" };

        // Act
        var result = controller.CreateAccount(newAccount);

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
        // Use reflection or dynamic to check success property if needed, 
        // but checking if the account is returned is also good.
        Assert.NotNull(jsonResult.Value);
        
        // Verify it was added
        var getResult = controller.GetAccounts();
        var getJsonResult = Assert.IsType<JsonResult>(getResult);
        var accounts = Assert.IsAssignableFrom<IEnumerable<Account>>(getJsonResult.Value);
        Assert.Contains(accounts, a => a.Name == "Test Account");
    }

    [Fact]
    public void UpdateAccount_UpdatesExistingAccount()
    {
        // Arrange
        var controller = CreateController();
        // First create an account to update
        var newAccount = new Account { Name = "To Update", Description = "Original" };
        var createResult = controller.CreateAccount(newAccount);
        var jsonResult = Assert.IsType<JsonResult>(createResult);
        var value = jsonResult.Value;
        var createdAccount = (Account)value?.GetType().GetProperty("account")?.GetValue(value)!;

        var updateInfo = new Account 
        { 
            Id = createdAccount.Id, 
            Name = "Updated Name", 
            Description = "Updated Description" 
        };

        // Act
        var result = controller.UpdateAccount(updateInfo);

        // Assert
        Assert.IsType<JsonResult>(result);
        
        var getResult = controller.GetAccounts();
        var getJsonResult = Assert.IsType<JsonResult>(getResult);
        var accounts = Assert.IsAssignableFrom<IEnumerable<Account>>(getJsonResult.Value);
        var updated = accounts.First(a => a.Id == createdAccount.Id);
        Assert.Equal("Updated Name", updated.Name);
        Assert.Equal("Updated Description", updated.Description);
    }

    [Fact]
    public void DeleteAccount_RemovesAccount()
    {
        // Arrange
        var controller = CreateController();
        var newAccount = new Account { Name = "To Delete" };
        var createResult = controller.CreateAccount(newAccount);
        var jsonResult = Assert.IsType<JsonResult>(createResult);
        var value = jsonResult.Value;
        var createdAccount = (Account)value?.GetType().GetProperty("account")?.GetValue(value)!;

        // Act
        var result = controller.DeleteAccount(createdAccount.Id);

        // Assert
        Assert.IsType<JsonResult>(result);
        
        var getResult = controller.GetAccounts();
        var getJsonResult = Assert.IsType<JsonResult>(getResult);
        var accounts = Assert.IsAssignableFrom<IEnumerable<Account>>(getJsonResult.Value);
        Assert.DoesNotContain(accounts, a => a.Id == createdAccount.Id);
    }

    [Fact]
    public void Layer_CRUD_Operations()
    {
        // Arrange
        var controller = CreateController();
        var layer = new Layer { Name = "Savings", AccountId = 1, IsActive = true };

        // Act & Assert - Create
        var createResult = controller.CreateLayer(layer);
        var createJson = Assert.IsType<JsonResult>(createResult);
        var createdLayer = (Layer)createJson.Value?.GetType().GetProperty("layer")?.GetValue(createJson.Value)!;
        Assert.Equal("Savings", createdLayer.Name);

        // Act & Assert - Get
        var getResult = controller.GetLayers();
        var layers = Assert.IsAssignableFrom<IEnumerable<Layer>>(Assert.IsType<JsonResult>(getResult).Value);
        Assert.Contains(layers, l => l.Name == "Savings");

        // Act & Assert - Update
        createdLayer.Name = "Emergency Fund";
        var updateResult = controller.UpdateLayer(createdLayer);
        Assert.IsType<JsonResult>(updateResult);
        
        getResult = controller.GetLayers();
        layers = Assert.IsAssignableFrom<IEnumerable<Layer>>(Assert.IsType<JsonResult>(getResult).Value);
        Assert.Contains(layers, l => l.Name == "Emergency Fund");

        // Act & Assert - Toggle
        var toggleResult = controller.ToggleLayer(createdLayer.Id, 1);
        var toggleJson = Assert.IsType<JsonResult>(toggleResult);
        var toggledLayer = (Layer)toggleJson.Value?.GetType().GetProperty("layer")?.GetValue(toggleJson.Value)!;
        Assert.False(toggledLayer.IsActive);

        // Act & Assert - Delete
        var deleteResult = controller.DeleteLayer(createdLayer.Id, 1);
        Assert.IsType<JsonResult>(deleteResult);
        getResult = controller.GetLayers();
        layers = Assert.IsAssignableFrom<IEnumerable<Layer>>(Assert.IsType<JsonResult>(getResult).Value);
        Assert.Empty(layers);
    }

    [Fact]
    public void Create_AddsExpense_AndReturnsSuccess()
    {
        // Arrange
        var controller = CreateController();
        var newItem = new Item 
        { 
            AccountId = 1, 
            Date = DateTime.Today, 
            Amount = 50.0m, 
            Description = "Test Expense",
            Type = TransactionType.Debit
        };

        // Act
        var result = controller.Create(newItem);

        // Assert
        Assert.IsType<JsonResult>(result);
        
        // Verify it was added
        var getResult = controller.GetAllExpenses(1);
        var getJsonResult = Assert.IsType<JsonResult>(getResult);
        var expenses = Assert.IsAssignableFrom<IEnumerable<Item>>(getJsonResult.Value);
        Assert.Contains(expenses, e => e.Description == "Test Expense" && e.Amount == 50.0m);
    }

    [Fact]
    public void GetDailyBalances_ReturnsBalancesForMonth()
    {
        // Arrange
        var controller = CreateController();
        var today = DateTime.Today;
        
        // Add an expense
        controller.Create(new Item 
        { 
            AccountId = 1, 
            Date = today, 
            Amount = 100.0m, 
            Description = "Rent",
            Type = TransactionType.Debit
        });

        // Act
        var result = controller.GetDailyBalances(today.Year, today.Month, 1);

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
        var balances = Assert.IsAssignableFrom<IDictionary<string, object>>(jsonResult.Value);
        Assert.NotEmpty(balances);
    }
    [Fact]
    public void GetDailyBalances_CalculatesCorrectBalance()
    {
        // Arrange
        var controller = CreateController();
        var today = DateTime.Today;
        
        // Get initial account
        var getAccountsResult = controller.GetAccounts();
        var accounts = Assert.IsAssignableFrom<IEnumerable<Account>>(Assert.IsType<JsonResult>(getAccountsResult).Value);
        var account = accounts.First(a => a.Id == 1);
        var initialBalance = account.StartingBalance;

        // Add an expense
        controller.Create(new Item 
        { 
            AccountId = 1, 
            Date = today, 
            Amount = 100.0m, 
            Description = "Test Expense",
            Type = TransactionType.Debit
        });

        // Act
        var result = controller.GetDailyBalances(today.Year, today.Month, 1);

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
        var balances = Assert.IsAssignableFrom<IDictionary<string, object>>(jsonResult.Value);
        
        var dateStr = today.ToString("yyyy-MM-dd");
        Assert.True(balances.ContainsKey(dateStr));
        
        var balanceEntry = balances[dateStr];
        var balanceValue = (decimal)balanceEntry.GetType().GetProperty("balance")?.GetValue(balanceEntry)!;
        
        Assert.Equal(initialBalance - 100.0m, balanceValue);
    }

    [Fact]
    public void RecurringItems_Generation_Works()
    {
        // Arrange
        var controller = CreateController();
        var start = new DateTime(2026, 1, 1);
        
        // Add a weekly recurring item
        controller.Create(new Item
        {
            AccountId = 1,
            Date = start,
            Amount = 50m,
            Description = "Weekly Gym",
            IsRecurring = true,
            RecurringInterval = 1,
            RecurringPeriod = "weeks",
            RecurringStartDate = start
        });

        // Act
        var result = controller.GetExpenses(2026, 1, 1); // January 2026

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
        var expenses = Assert.IsAssignableFrom<IEnumerable<Item>>(jsonResult.Value).ToList();
        
        // In January 2026, there are 5 Thursdays (Jan 1, 8, 15, 22, 29)
        Assert.Equal(5, expenses.Count);
        Assert.All(expenses, e => Assert.Equal("Weekly Gym", e.Description));
        Assert.Contains(expenses, e => e.Date == new DateTime(2026, 1, 29));
    }

    [Fact]
    public void UpdateRecurring_ThisOne_CreatesException()
    {
        // Arrange
        var controller = CreateController();
        var start = new DateTime(2026, 1, 1);
        var recurring = new Item
        {
            AccountId = 1,
            Date = start,
            Amount = 50m,
            Description = "Weekly Gym",
            IsRecurring = true,
            RecurringInterval = 1,
            RecurringPeriod = "weeks",
            RecurringStartDate = start
        };
        controller.Create(recurring);
        
        // First occurrence is Jan 1. We want to edit Jan 8 instance.
        var targetDate = new DateTime(2026, 1, 8);
        var updateInfo = new Item
        {
            Id = 1, // Parent Id
            AccountId = 1,
            Date = targetDate,
            Amount = 60m,
            Description = "Weekly Gym (Special)",
            Type = TransactionType.Debit
        };

        // Act
        controller.UpdateRecurring(updateInfo, "ThisOne");

        // Assert
        var result = controller.GetExpenses(2026, 1, 1);
        var expenses = Assert.IsAssignableFrom<IEnumerable<Item>>(Assert.IsType<JsonResult>(result).Value).ToList();
        
        // Should still have 5 items
        Assert.Equal(5, expenses.Count);
        
        var special = expenses.First(e => e.Date == targetDate);
        Assert.Equal(60m, special.Amount);
        Assert.Equal("Weekly Gym (Special)", special.Description);
        Assert.True(special.IsException);

        var nextOne = expenses.First(e => e.Date == new DateTime(2026, 1, 15));
        Assert.Equal(50m, nextOne.Amount);
        Assert.Equal("Weekly Gym", nextOne.Description);
    }

    [Fact]
    public void DeleteRecurring_FromThisOne_EndsSeries()
    {
        // Arrange
        var controller = CreateController();
        var start = new DateTime(2026, 1, 1);
        controller.Create(new Item
        {
            AccountId = 1,
            Date = start,
            Amount = 50m,
            Description = "Weekly Gym",
            IsRecurring = true,
            RecurringInterval = 1,
            RecurringPeriod = "weeks",
            RecurringStartDate = start
        });

        // Act - Delete from Jan 15 onwards
        controller.DeleteRecurring(1, 1, "FromThisOne", new DateTime(2026, 1, 15));

        // Assert
        var result = controller.GetExpenses(2026, 1, 1);
        var expenses = Assert.IsAssignableFrom<IEnumerable<Item>>(Assert.IsType<JsonResult>(result).Value).ToList();
        
        // Should only have Jan 1 and Jan 8
        Assert.Equal(2, expenses.Count);
        Assert.Contains(expenses, e => e.Date == new DateTime(2026, 1, 1));
        Assert.Contains(expenses, e => e.Date == new DateTime(2026, 1, 8));
        Assert.DoesNotContain(expenses, e => e.Date == new DateTime(2026, 1, 15));
    }

    [Fact]
    public void BalanceOverride_AffectsDailyBalances()
    {
        // Arrange
        var controller = CreateController();
        var today = DateTime.Today;
        var dateStr = today.ToString("yyyy-MM-dd");
        
        // Act
        controller.SetBalanceOverride(new ExpenseController.BalanceOverrideRequest 
        { 
            AccountId = 1, 
            Date = dateStr, 
            Balance = 5000m 
        });

        // Assert
        var result = controller.GetDailyBalances(today.Year, today.Month, 1);
        var balances = Assert.IsAssignableFrom<IDictionary<string, object>>(Assert.IsType<JsonResult>(result).Value);
        
        var balanceEntry = balances[dateStr];
        var balanceValue = (decimal)balanceEntry.GetType().GetProperty("balance")?.GetValue(balanceEntry)!;
        Assert.Equal(5000m, balanceValue);
    }

    [Fact]
    public void LoadFromStorage_RestoresState()
    {
        // Arrange
        var controller = CreateController();
        var request = new ExpenseController.LoadStorageRequest
        {
            Accounts = new List<Account> { new Account { Id = 10, Name = "Stored Account" } },
            Items = new List<Item> { new Item { Id = 100, AccountId = 10, Description = "Stored Item", Date = DateTime.Today } },
            Layers = new List<Layer> { new Layer { Id = 5, AccountId = 10, Name = "Stored Layer" } },
            BalanceOverrides = new Dictionary<int, Dictionary<string, decimal>> { { 10, new Dictionary<string, decimal> { { DateTime.Today.ToString("yyyy-MM-dd"), 1234m } } } }
        };

        // Act
        controller.LoadFromStorage(request);

        // Assert
        var accounts = Assert.IsAssignableFrom<IEnumerable<Account>>(Assert.IsType<JsonResult>(controller.GetAccounts()).Value);
        Assert.Single(accounts, a => a.Id == 10);
        
        var items = Assert.IsAssignableFrom<IEnumerable<Item>>(Assert.IsType<JsonResult>(controller.GetAllExpenses(10)).Value);
        Assert.Single(items, i => i.Id == 100);

        var layers = Assert.IsAssignableFrom<IEnumerable<Layer>>(Assert.IsType<JsonResult>(controller.GetLayers()).Value);
        Assert.Single(layers, l => l.Id == 5);
    }

    [Fact]
    public void Monthly_RecurringItems_Generation_Works()
    {
        // Arrange
        var controller = CreateController();
        var start = new DateTime(2026, 1, 15);
        
        controller.Create(new Item
        {
            AccountId = 1,
            Date = start,
            Amount = 100m,
            Description = "Rent",
            IsRecurring = true,
            RecurringInterval = 1,
            RecurringPeriod = "months",
            RecurringStartDate = start
        });

        // Act
        var result = controller.GetExpenses(2026, 3, 1); // March 2026

        // Assert
        var items = Assert.IsAssignableFrom<IEnumerable<Item>>(Assert.IsType<JsonResult>(result).Value).ToList();
        Assert.Single(items);
        Assert.Equal(new DateTime(2026, 3, 15), items[0].Date);
    }

    [Fact]
    public void UpdateRecurring_AllInSeries_UpdatesParent()
    {
        // Arrange
        var controller = CreateController();
        var start = new DateTime(2026, 1, 1);
        controller.Create(new Item
        {
            AccountId = 1,
            Date = start,
            Amount = 50m,
            Description = "Old Name",
            IsRecurring = true,
            RecurringInterval = 1,
            RecurringPeriod = "weeks",
            RecurringStartDate = start
        });

        var updateInfo = new Item
        {
            Id = 1,
            AccountId = 1,
            Amount = 75m,
            Description = "New Name",
            RecurringInterval = 1,
            RecurringPeriod = "weeks"
        };

        // Act
        controller.UpdateRecurring(updateInfo, "AllInSeries");

        // Assert
        var items = Assert.IsAssignableFrom<IEnumerable<Item>>(Assert.IsType<JsonResult>(controller.GetAllExpenses(1)).Value).ToList();
        var parent = items.First(i => i.Id == 1);
        Assert.Equal("New Name", parent.Description);
        Assert.Equal(75m, parent.Amount);
    }

    [Fact]
    public void DeleteRecurring_ThisOne_CreatesDeletedException()
    {
        // Arrange
        var controller = CreateController();
        var start = new DateTime(2026, 1, 1);
        controller.Create(new Item
        {
            AccountId = 1,
            Date = start,
            Amount = 50m,
            Description = "Weekly Gym",
            IsRecurring = true,
            RecurringInterval = 1,
            RecurringPeriod = "weeks",
            RecurringStartDate = start
        });

        // Act - Delete Jan 8 instance
        controller.DeleteRecurring(1, 1, "ThisOne", new DateTime(2026, 1, 8));

        // Assert
        var result = controller.GetExpenses(2026, 1, 1);
        var expenses = Assert.IsAssignableFrom<IEnumerable<Item>>(Assert.IsType<JsonResult>(result).Value).ToList();
        
        // Should have 4 items (Jan 1, 15, 22, 29) - Jan 8 is gone
        Assert.Equal(4, expenses.Count);
        Assert.DoesNotContain(expenses, e => e.Date == new DateTime(2026, 1, 8));
        
        // Check that an exception was created in the database
        var allItems = Assert.IsAssignableFrom<IEnumerable<Item>>(Assert.IsType<JsonResult>(controller.GetAllExpenses(1)).Value).ToList();
        Assert.Contains(allItems, e => e.IsException && e.Description == "[DELETED]" && e.OriginalDate == new DateTime(2026, 1, 8));
    }

    [Fact]
    public void Layers_Filter_Expenses()
    {
        // Arrange
        var controller = CreateController();
        
        // Create a layer
        var createLayerResult = controller.CreateLayer(new Layer { AccountId = 1, Name = "Hidden Layer", IsActive = false });
        var createdLayer = (Layer)Assert.IsType<JsonResult>(createLayerResult).Value?.GetType().GetProperty("layer")?.GetValue(Assert.IsType<JsonResult>(createLayerResult).Value)!;

        // Add expense to that layer
        controller.Create(new Item { AccountId = 1, Date = DateTime.Today, Amount = 100m, Description = "Layered Expense", LayerId = createdLayer.Id });
        
        // Add regular expense
        controller.Create(new Item { AccountId = 1, Date = DateTime.Today, Amount = 50m, Description = "Regular Expense" });

        // Act
        var result = controller.GetExpenses(DateTime.Today.Year, DateTime.Today.Month, 1);

        // Assert
        var expenses = Assert.IsAssignableFrom<IEnumerable<Item>>(Assert.IsType<JsonResult>(result).Value).ToList();
        Assert.Single(expenses);
        Assert.Equal("Regular Expense", expenses[0].Description);

        // Act - Toggle layer
        controller.ToggleLayer(createdLayer.Id, 1);
        result = controller.GetExpenses(DateTime.Today.Year, DateTime.Today.Month, 1);
        expenses = Assert.IsAssignableFrom<IEnumerable<Item>>(Assert.IsType<JsonResult>(result).Value).ToList();
        
        // Assert
        Assert.Equal(2, expenses.Count);
        Assert.Contains(expenses, e => e.Description == "Layered Expense");
    }

    [Fact]
    public void UpdateRecurring_FromThisOne_SplitsSeries()
    {
        // Arrange
        var controller = CreateController();
        var start = new DateTime(2026, 1, 1);
        controller.Create(new Item
        {
            AccountId = 1,
            Date = start,
            Amount = 50m,
            Description = "Old Series",
            IsRecurring = true,
            RecurringInterval = 1,
            RecurringPeriod = "weeks",
            RecurringStartDate = start
        });

        // Act - Update from Jan 15 onwards
        var targetDate = new DateTime(2026, 1, 15);
        var updateInfo = new Item
        {
            Id = 1,
            AccountId = 1,
            Date = targetDate,
            Amount = 100m,
            Description = "New Series",
            RecurringInterval = 1,
            RecurringPeriod = "weeks"
        };
        controller.UpdateRecurring(updateInfo, "FromThisOne");

        // Assert
        var result = controller.GetExpenses(2026, 1, 1);
        var expenses = Assert.IsAssignableFrom<IEnumerable<Item>>(Assert.IsType<JsonResult>(result).Value).ToList();
        
        // Jan 1, 8 -> Old Series (50m)
        // Jan 15, 22, 29 -> New Series (100m)
        Assert.Equal(5, expenses.Count);
        
        var oldOnes = expenses.Where(e => e.Description == "Old Series").ToList();
        Assert.Equal(2, oldOnes.Count);
        Assert.All(oldOnes, e => Assert.Equal(50m, e.Amount));

        var newOnes = expenses.Where(e => e.Description == "New Series").ToList();
        Assert.Equal(3, newOnes.Count);
        Assert.All(newOnes, e => Assert.Equal(100m, e.Amount));
    }
}
