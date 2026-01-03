using BudgetCal.Controllers;
using BudgetCal.Models;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace BudgetCal.Tests;

[Collection("ExpenseController Tests")]
public class ExpenseControllerEdgeCaseTests
{
    private ExpenseController CreateController()
    {
        var controller = new ExpenseController();
        controller.ClearAll();
        return controller;
    }

    [Fact]
    public void UpdateRecurring_InvalidMode_ReturnsBadRequest()
    {
        // Arrange
        var controller = CreateController();
        controller.Create(new Item { Id = 1, AccountId = 1, Date = DateTime.Today });
        
        // Act & Assert
        Assert.Throws<ArgumentException>(() => controller.UpdateRecurring(new Item { Id = 1, AccountId = 1 }, "InvalidMode"));
    }

    [Fact]
    public void DeleteRecurring_InvalidMode_ThrowsArgumentException()
    {
        // Arrange
        var controller = CreateController();
        controller.Create(new Item { Id = 1, AccountId = 1, Date = DateTime.Today });
        
        // Act & Assert
        Assert.Throws<ArgumentException>(() => controller.DeleteRecurring(1, 1, "InvalidMode", DateTime.Today));
    }

    #region Update and Delete Basic Operations

    [Fact]
    public void Update_UpdatesExistingItem()
    {
        // Arrange
        var controller = CreateController();
        var item = new Item { AccountId = 1, Date = DateTime.Today, Amount = 50m, Description = "Original" };
        var createResult = controller.Create(item);
        var createJsonResult = Assert.IsType<JsonResult>(createResult);
        var createdItem = (Item)createJsonResult.Value?.GetType().GetProperty("expense")?.GetValue(createJsonResult.Value)!;

        var updatedItem = new Item
        {
            Id = createdItem.Id,
            AccountId = 1,
            Date = DateTime.Today.AddDays(1),
            Amount = 100m,
            Description = "Updated",
            Type = TransactionType.Credit
        };

        // Act
        var result = controller.Update(updatedItem);

        // Assert
        var updateJsonResult = Assert.IsType<JsonResult>(result);
        var returned = Assert.IsType<Item>(updateJsonResult.Value);
        Assert.Equal("Updated", returned.Description);
        Assert.Equal(100m, returned.Amount);
        Assert.Equal(TransactionType.Credit, returned.Type);
    }

    [Fact]
    public void Update_ReturnsNotFound_WhenItemDoesNotExist()
    {
        // Arrange
        var controller = CreateController();
        var nonExistentItem = new Item { Id = 999, AccountId = 1, Date = DateTime.Today, Amount = 50m };

        // Act
        var result = controller.Update(nonExistentItem);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void Delete_RemovesItem()
    {
        // Arrange
        var controller = CreateController();
        var item = new Item { AccountId = 1, Date = DateTime.Today, Amount = 50m, Description = "To Delete" };
        var createResult = controller.Create(item);
        var jsonResult = Assert.IsType<JsonResult>(createResult);
        var createdItem = (Item)jsonResult.Value?.GetType().GetProperty("expense")?.GetValue(jsonResult.Value)!;

        // Act
        var result = controller.Delete(createdItem.Id, 1);

        // Assert
        Assert.IsType<OkResult>(result);
        
        var getResult = controller.GetAllExpenses(1);
        var items = Assert.IsAssignableFrom<IEnumerable<Item>>(Assert.IsType<JsonResult>(getResult).Value);
        Assert.DoesNotContain(items, i => i.Id == createdItem.Id);
    }

    [Fact]
    public void Delete_ReturnsNotFound_WhenItemDoesNotExist()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = controller.Delete(999, 1);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    #endregion

    #region Account Edge Cases

    [Fact]
    public void UpdateAccount_ReturnsNotFound_WhenAccountDoesNotExist()
    {
        // Arrange
        var controller = CreateController();
        var nonExistentAccount = new Account { Id = 999, Name = "Does Not Exist" };

        // Act
        var result = controller.UpdateAccount(nonExistentAccount);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void DeleteAccount_ReturnsNotFound_WhenAccountDoesNotExist()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = controller.DeleteAccount(999);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void DeleteAccount_RemovesAssociatedItemsAndLayers()
    {
        // Arrange
        var controller = CreateController();
        var account = new Account { Name = "To Delete" };
        var createAccountResult = controller.CreateAccount(account);
        var jsonResult = Assert.IsType<JsonResult>(createAccountResult);
        var createdAccount = (Account)jsonResult.Value?.GetType().GetProperty("account")?.GetValue(jsonResult.Value)!;

        // Add items and layers
        controller.Create(new Item { AccountId = createdAccount.Id, Date = DateTime.Today, Amount = 50m });
        controller.CreateLayer(new Layer { AccountId = createdAccount.Id, Name = "Test Layer" });

        // Act
        controller.DeleteAccount(createdAccount.Id);

        // Assert
        var items = Assert.IsAssignableFrom<IEnumerable<Item>>(Assert.IsType<JsonResult>(controller.GetAllExpenses(createdAccount.Id)).Value);
        var layers = Assert.IsAssignableFrom<IEnumerable<Layer>>(Assert.IsType<JsonResult>(controller.GetLayers()).Value);
        
        Assert.Empty(items);
        Assert.DoesNotContain(layers, l => l.AccountId == createdAccount.Id);
    }

    #endregion

    #region Layer Edge Cases

    [Fact]
    public void UpdateLayer_ReturnsNotFound_WhenLayerDoesNotExist()
    {
        // Arrange
        var controller = CreateController();
        var nonExistentLayer = new Layer { Id = 999, AccountId = 1, Name = "Does Not Exist" };

        // Act
        var result = controller.UpdateLayer(nonExistentLayer);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void ToggleLayer_ReturnsNotFound_WhenLayerDoesNotExist()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = controller.ToggleLayer(999, 1);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void DeleteLayer_ReturnsNotFound_WhenLayerDoesNotExist()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = controller.DeleteLayer(999, 1);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void MultipleLayers_WithSameAccount_WorkIndependently()
    {
        // Arrange
        var controller = CreateController();
        var layer1Result = Assert.IsType<JsonResult>(controller.CreateLayer(new Layer { AccountId = 1, Name = "Layer 1", IsActive = true }));
        var layer1 = (Layer)layer1Result.Value?.GetType().GetProperty("layer")?.GetValue(layer1Result.Value)!;
        
        var layer2Result = Assert.IsType<JsonResult>(controller.CreateLayer(new Layer { AccountId = 1, Name = "Layer 2", IsActive = false }));
        var layer2 = (Layer)layer2Result.Value?.GetType().GetProperty("layer")?.GetValue(layer2Result.Value)!;

        controller.Create(new Item { AccountId = 1, Date = DateTime.Today, Amount = 50m, Description = "Item in Layer 1", LayerId = layer1.Id });
        controller.Create(new Item { AccountId = 1, Date = DateTime.Today, Amount = 75m, Description = "Item in Layer 2", LayerId = layer2.Id });

        // Act
        var result = controller.GetExpenses(DateTime.Today.Year, DateTime.Today.Month, 1);
        var items = Assert.IsAssignableFrom<IEnumerable<Item>>(Assert.IsType<JsonResult>(result).Value).ToList();

        // Assert
        Assert.Single(items);
        Assert.Equal("Item in Layer 1", items[0].Description);
    }

    #endregion

    #region Credit Transactions

    [Fact]
    public void Credit_Transaction_IncreasesBalance()
    {
        // Arrange
        var controller = CreateController();
        var account = Assert.IsAssignableFrom<IEnumerable<Account>>(Assert.IsType<JsonResult>(controller.GetAccounts()).Value).First();
        var initialBalance = account.StartingBalance;

        // Act
        controller.Create(new Item
        {
            AccountId = 1,
            Date = DateTime.Today,
            Amount = 500m,
            Description = "Income",
            Type = TransactionType.Credit
        });

        // Assert
        var result = controller.GetDailyBalances(DateTime.Today.Year, DateTime.Today.Month, 1);
        var balances = Assert.IsAssignableFrom<IDictionary<string, object>>(Assert.IsType<JsonResult>(result).Value);
        var todayBalance = (decimal)balances[DateTime.Today.ToString("yyyy-MM-dd")].GetType().GetProperty("balance")?.GetValue(balances[DateTime.Today.ToString("yyyy-MM-dd")])!;
        
        Assert.Equal(initialBalance + 500m, todayBalance);
    }

    [Fact]
    public void Mixed_DebitAndCredit_CalculatesCorrectBalance()
    {
        // Arrange
        var controller = CreateController();
        var account = Assert.IsAssignableFrom<IEnumerable<Account>>(Assert.IsType<JsonResult>(controller.GetAccounts()).Value).First();
        var initialBalance = account.StartingBalance;

        // Act
        controller.Create(new Item { AccountId = 1, Date = DateTime.Today, Amount = 100m, Type = TransactionType.Debit });
        controller.Create(new Item { AccountId = 1, Date = DateTime.Today, Amount = 200m, Type = TransactionType.Credit });
        controller.Create(new Item { AccountId = 1, Date = DateTime.Today, Amount = 50m, Type = TransactionType.Debit });

        // Assert
        var result = controller.GetDailyBalances(DateTime.Today.Year, DateTime.Today.Month, 1);
        var balances = Assert.IsAssignableFrom<IDictionary<string, object>>(Assert.IsType<JsonResult>(result).Value);
        var todayBalance = (decimal)balances[DateTime.Today.ToString("yyyy-MM-dd")].GetType().GetProperty("balance")?.GetValue(balances[DateTime.Today.ToString("yyyy-MM-dd")])!;
        
        // 1000 - 100 + 200 - 50 = 1050
        Assert.Equal(initialBalance - 100m + 200m - 50m, todayBalance);
    }

    #endregion

    #region Recurring Items - Additional Periods

    [Fact]
    public void Daily_RecurringItems_Generation_Works()
    {
        // Arrange
        var controller = CreateController();
        var start = new DateTime(2026, 1, 1);

        controller.Create(new Item
        {
            AccountId = 1,
            Date = start,
            Amount = 10m,
            Description = "Daily Expense",
            IsRecurring = true,
            RecurringInterval = 1,
            RecurringPeriod = "days",
            RecurringStartDate = start
        });

        // Act - Get first week of January
        var result = controller.GetExpenses(2026, 1, 1);

        // Assert
        var items = Assert.IsAssignableFrom<IEnumerable<Item>>(Assert.IsType<JsonResult>(result).Value).ToList();
        Assert.Equal(31, items.Count); // All days in January
    }

    [Fact]
    public void Yearly_RecurringItems_Generation_Works()
    {
        // Arrange
        var controller = CreateController();
        var start = new DateTime(2024, 1, 15);

        controller.Create(new Item
        {
            AccountId = 1,
            Date = start,
            Amount = 100m,
            Description = "Annual Fee",
            IsRecurring = true,
            RecurringInterval = 1,
            RecurringPeriod = "years",
            RecurringStartDate = start
        });

        // Act
        var result = controller.GetExpenses(2026, 1, 1);

        // Assert
        var items = Assert.IsAssignableFrom<IEnumerable<Item>>(Assert.IsType<JsonResult>(result).Value).ToList();
        Assert.Single(items);
        Assert.Equal(new DateTime(2026, 1, 15), items[0].Date);
    }

    [Fact]
    public void RecurringEndDate_StopsGeneration()
    {
        // Arrange
        var controller = CreateController();
        var start = new DateTime(2026, 1, 1);
        var end = new DateTime(2026, 1, 15);

        controller.Create(new Item
        {
            AccountId = 1,
            Date = start,
            Amount = 50m,
            Description = "Limited Series",
            IsRecurring = true,
            RecurringInterval = 1,
            RecurringPeriod = "weeks",
            RecurringStartDate = start,
            RecurringEndDate = end
        });

        // Act
        var result = controller.GetExpenses(2026, 1, 1);

        // Assert
        var items = Assert.IsAssignableFrom<IEnumerable<Item>>(Assert.IsType<JsonResult>(result).Value).ToList();
        Assert.Equal(3, items.Count); // Jan 1, 8, 15 (ends on 15)
        Assert.DoesNotContain(items, i => i.Date > end);
    }

    [Fact]
    public void RecurringItem_WithEndDateBeforeStartDate_DoesNotGenerate()
    {
        // Arrange
        var controller = CreateController();
        var start = new DateTime(2026, 1, 15);

        controller.Create(new Item
        {
            AccountId = 1,
            Date = start,
            Amount = 50m,
            Description = "Expired Series",
            IsRecurring = true,
            RecurringInterval = 1,
            RecurringPeriod = "weeks",
            RecurringStartDate = start,
            RecurringEndDate = new DateTime(2026, 1, 10) // Before start
        });

        // Act
        var result = controller.GetExpenses(2026, 1, 1);

        // Assert
        var items = Assert.IsAssignableFrom<IEnumerable<Item>>(Assert.IsType<JsonResult>(result).Value).ToList();
        Assert.Empty(items);
    }

    #endregion

    #region Balance Calculations

    [Fact]
    public void Balance_BeforeAccountStartDate_ReturnsZero()
    {
        // Arrange
        var controller = CreateController();
        var accounts = Assert.IsAssignableFrom<IEnumerable<Account>>(Assert.IsType<JsonResult>(controller.GetAccounts()).Value);
        var account = accounts.First();

        // Act - Get balance for a date before account start
        var beforeDate = account.StartDate.AddDays(-1);
        var result = controller.GetDailyBalances(beforeDate.Year, beforeDate.Month, 1);

        // Assert
        var balances = Assert.IsAssignableFrom<IDictionary<string, object>>(Assert.IsType<JsonResult>(result).Value);
        var dateStr = beforeDate.ToString("yyyy-MM-dd");
        
        if (balances.ContainsKey(dateStr))
        {
            var balance = (decimal)balances[dateStr].GetType().GetProperty("balance")?.GetValue(balances[dateStr])!;
            Assert.Equal(0m, balance);
        }
    }

    [Fact]
    public void MultipleAccounts_HaveIndependentBalances()
    {
        // Arrange
        var controller = CreateController();
        var createResult = Assert.IsType<JsonResult>(controller.CreateAccount(new Account { Name = "Account 2", StartingBalance = 5000m }));
        var account2 = (Account)createResult.Value?.GetType().GetProperty("account")?.GetValue(createResult.Value)!;

        controller.Create(new Item { AccountId = 1, Date = DateTime.Today, Amount = 100m, Type = TransactionType.Debit });
        controller.Create(new Item { AccountId = account2.Id, Date = DateTime.Today, Amount = 200m, Type = TransactionType.Debit });

        // Act
        var result1 = controller.GetDailyBalances(DateTime.Today.Year, DateTime.Today.Month, 1);
        var result2 = controller.GetDailyBalances(DateTime.Today.Year, DateTime.Today.Month, account2.Id);

        // Assert
        var balances1 = Assert.IsAssignableFrom<IDictionary<string, object>>(Assert.IsType<JsonResult>(result1).Value);
        var balances2 = Assert.IsAssignableFrom<IDictionary<string, object>>(Assert.IsType<JsonResult>(result2).Value);
        
        var dateStr = DateTime.Today.ToString("yyyy-MM-dd");
        var balance1 = (decimal)balances1[dateStr].GetType().GetProperty("balance")?.GetValue(balances1[dateStr])!;
        var balance2 = (decimal)balances2[dateStr].GetType().GetProperty("balance")?.GetValue(balances2[dateStr])!;

        Assert.Equal(900m, balance1); // 1000 - 100
        Assert.Equal(4800m, balance2); // 5000 - 200
    }

    [Fact]
    public void GetAllData_ReturnsCompleteState()
    {
        // Arrange
        var controller = CreateController();
        controller.Create(new Item { AccountId = 1, Date = DateTime.Today, Amount = 50m });
        controller.CreateLayer(new Layer { AccountId = 1, Name = "Test" });
        controller.SetBalanceOverride(new ExpenseController.BalanceOverrideRequest { AccountId = 1, Date = DateTime.Today.ToString("yyyy-MM-dd"), Balance = 1000m });

        // Act
        var result = controller.GetAllData();

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
        var data = jsonResult.Value;
        
        Assert.NotNull(data?.GetType().GetProperty("accounts")?.GetValue(data));
        Assert.NotNull(data?.GetType().GetProperty("expenses")?.GetValue(data));
        Assert.NotNull(data?.GetType().GetProperty("layers")?.GetValue(data));
        Assert.NotNull(data?.GetType().GetProperty("balanceOverrides")?.GetValue(data));
    }

    #endregion

    #region Edge Cases for GetExpenses

    [Fact]
    public void GetExpenses_EmptyMonth_ReturnsEmptyList()
    {
        // Arrange
        var controller = CreateController();

        // Act - Get expenses for a month with no items
        var result = controller.GetExpenses(2030, 12, 1);

        // Assert
        var items = Assert.IsAssignableFrom<IEnumerable<Item>>(Assert.IsType<JsonResult>(result).Value);
        Assert.Empty(items);
    }

    [Fact]
    public void GetExpenses_FiltersByAccountId()
    {
        // Arrange
        var controller = CreateController();
        var createAccountResult = Assert.IsType<JsonResult>(controller.CreateAccount(new Account { Name = "Account 2" }));
        var account2 = (Account)createAccountResult.Value?.GetType().GetProperty("account")?.GetValue(createAccountResult.Value)!;

        controller.Create(new Item { AccountId = 1, Date = DateTime.Today, Amount = 100m, Description = "Account 1 Item" });
        controller.Create(new Item { AccountId = account2.Id, Date = DateTime.Today, Amount = 200m, Description = "Account 2 Item" });

        // Act
        var result1 = controller.GetExpenses(DateTime.Today.Year, DateTime.Today.Month, 1);
        var result2 = controller.GetExpenses(DateTime.Today.Year, DateTime.Today.Month, account2.Id);

        // Assert
        var items1 = Assert.IsAssignableFrom<IEnumerable<Item>>(Assert.IsType<JsonResult>(result1).Value).ToList();
        var items2 = Assert.IsAssignableFrom<IEnumerable<Item>>(Assert.IsType<JsonResult>(result2).Value).ToList();

        Assert.Single(items1);
        Assert.Equal("Account 1 Item", items1[0].Description);
        
        Assert.Single(items2);
        Assert.Equal("Account 2 Item", items2[0].Description);
    }

    #endregion

    #region Recurring Items - Complex Scenarios

    [Fact]
    public void RecurringItem_StartingMidMonth_GeneratesCorrectly()
    {
        // Arrange
        var controller = CreateController();
        var start = new DateTime(2026, 1, 15);

        controller.Create(new Item
        {
            AccountId = 1,
            Date = start,
            Amount = 50m,
            Description = "Mid-Month Start",
            IsRecurring = true,
            RecurringInterval = 1,
            RecurringPeriod = "weeks",
            RecurringStartDate = start
        });

        // Act
        var result = controller.GetExpenses(2026, 1, 1);

        // Assert
        var items = Assert.IsAssignableFrom<IEnumerable<Item>>(Assert.IsType<JsonResult>(result).Value).ToList();
        Assert.Equal(3, items.Count); // Jan 15, 22, 29
        Assert.All(items, i => Assert.True(i.Date >= start));
    }

    [Fact]
    public void RecurringItem_WithInterval_GreaterThanOne()
    {
        // Arrange
        var controller = CreateController();
        var start = new DateTime(2026, 1, 1);

        controller.Create(new Item
        {
            AccountId = 1,
            Date = start,
            Amount = 50m,
            Description = "Every 2 Weeks",
            IsRecurring = true,
            RecurringInterval = 2,
            RecurringPeriod = "weeks",
            RecurringStartDate = start
        });

        // Act
        var result = controller.GetExpenses(2026, 1, 1);

        // Assert
        var items = Assert.IsAssignableFrom<IEnumerable<Item>>(Assert.IsType<JsonResult>(result).Value).ToList();
        Assert.Equal(3, items.Count); // Jan 1, 15, 29
        Assert.Equal(new DateTime(2026, 1, 1), items[0].Date);
        Assert.Equal(new DateTime(2026, 1, 15), items[1].Date);
        Assert.Equal(new DateTime(2026, 1, 29), items[2].Date);
    }

    [Fact]
    public void DeleteRecurring_AllInSeries_RemovesExceptions()
    {
        // Arrange
        var controller = CreateController();
        var start = new DateTime(2026, 1, 1);
        controller.Create(new Item
        {
            AccountId = 1,
            Date = start,
            Amount = 50m,
            Description = "Series",
            IsRecurring = true,
            RecurringInterval = 1,
            RecurringPeriod = "weeks",
            RecurringStartDate = start
        });

        // Create an exception
        controller.UpdateRecurring(new Item
        {
            Id = 1,
            AccountId = 1,
            Date = new DateTime(2026, 1, 8),
            Amount = 100m,
            Description = "Exception"
        }, "ThisOne");

        // Act - Delete entire series
        controller.DeleteRecurring(1, 1, "AllInSeries", start);

        // Assert
        var allItems = Assert.IsAssignableFrom<IEnumerable<Item>>(Assert.IsType<JsonResult>(controller.GetAllExpenses(1)).Value);
        Assert.Empty(allItems);
    }

    #endregion

    #region Balance Override Edge Cases

    [Fact]
    public void BalanceOverride_WithMultipleOverrides_UsesCorrectOne()
    {
        // Arrange
        var controller = CreateController();
        var date1 = new DateTime(2026, 1, 10);
        var date2 = new DateTime(2026, 1, 20);

        controller.SetBalanceOverride(new ExpenseController.BalanceOverrideRequest { AccountId = 1, Date = date1.ToString("yyyy-MM-dd"), Balance = 2000m });
        controller.SetBalanceOverride(new ExpenseController.BalanceOverrideRequest { AccountId = 1, Date = date2.ToString("yyyy-MM-dd"), Balance = 3000m });

        // Act
        var result = controller.GetDailyBalances(2026, 1, 1);

        // Assert
        var balances = Assert.IsAssignableFrom<IDictionary<string, object>>(Assert.IsType<JsonResult>(result).Value);
        
        var balance1 = (decimal)balances[date1.ToString("yyyy-MM-dd")].GetType().GetProperty("balance")?.GetValue(balances[date1.ToString("yyyy-MM-dd")])!;
        var balance2 = (decimal)balances[date2.ToString("yyyy-MM-dd")].GetType().GetProperty("balance")?.GetValue(balances[date2.ToString("yyyy-MM-dd")])!;
        
        Assert.Equal(2000m, balance1);
        Assert.Equal(3000m, balance2);
    }

    [Fact]
    public void BalanceOverride_CalculatesSubsequentDaysCorrectly()
    {
        // Arrange
        var controller = CreateController();
        var overrideDate = new DateTime(2026, 1, 10);
        
        controller.SetBalanceOverride(new ExpenseController.BalanceOverrideRequest { AccountId = 1, Date = overrideDate.ToString("yyyy-MM-dd"), Balance = 2000m });
        controller.Create(new Item { AccountId = 1, Date = new DateTime(2026, 1, 11), Amount = 100m, Type = TransactionType.Debit });

        // Act
        var result = controller.GetDailyBalances(2026, 1, 1);

        // Assert
        var balances = Assert.IsAssignableFrom<IDictionary<string, object>>(Assert.IsType<JsonResult>(result).Value);
        var balanceAfterOverride = (decimal)balances["2026-01-11"].GetType().GetProperty("balance")?.GetValue(balances["2026-01-11"])!;
        
        Assert.Equal(1900m, balanceAfterOverride); // 2000 - 100
    }

    #endregion
}
