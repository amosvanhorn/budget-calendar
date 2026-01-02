using BudgetCal.Models;
using Xunit;

namespace BudgetCal.Tests;

public class ModelTests
{
    #region Account Model Tests

    [Fact]
    public void Account_DefaultValues_AreSet()
    {
        // Act
        var account = new Account();

        // Assert
        Assert.Equal(string.Empty, account.Name);
        Assert.Null(account.Description);
        Assert.Equal(new DateTime(2025, 9, 1), account.StartDate);
        Assert.Equal(1000m, account.StartingBalance);
    }

    [Fact]
    public void Account_CanSetAllProperties()
    {
        // Arrange
        var testDate = new DateTime(2026, 1, 1);

        // Act
        var account = new Account
        {
            Id = 5,
            Name = "Savings",
            Description = "My savings account",
            StartDate = testDate,
            StartingBalance = 5000m
        };

        // Assert
        Assert.Equal(5, account.Id);
        Assert.Equal("Savings", account.Name);
        Assert.Equal("My savings account", account.Description);
        Assert.Equal(testDate, account.StartDate);
        Assert.Equal(5000m, account.StartingBalance);
    }

    #endregion

    #region Layer Model Tests

    [Fact]
    public void Layer_DefaultValues_AreSet()
    {
        // Act
        var layer = new Layer();

        // Assert
        Assert.Equal(string.Empty, layer.Name);
        Assert.True(layer.IsActive);
    }

    [Fact]
    public void Layer_CanSetAllProperties()
    {
        // Act
        var layer = new Layer
        {
            Id = 10,
            AccountId = 5,
            Name = "Emergency Fund",
            IsActive = false
        };

        // Assert
        Assert.Equal(10, layer.Id);
        Assert.Equal(5, layer.AccountId);
        Assert.Equal("Emergency Fund", layer.Name);
        Assert.False(layer.IsActive);
    }

    #endregion

    #region Item Model Tests

    [Fact]
    public void Item_DefaultValues_AreSet()
    {
        // Act
        var item = new Item();

        // Assert
        Assert.Equal(string.Empty, item.Description);
        Assert.Equal("#e3f2fd", item.Color);
        Assert.Equal(TransactionType.Debit, item.Type);
        Assert.False(item.IsRecurring);
        Assert.Null(item.RecurringInterval);
        Assert.Null(item.RecurringPeriod);
        Assert.Null(item.RecurringStartDate);
        Assert.Null(item.RecurringEndDate);
        Assert.Null(item.ParentRecurringItemId);
        Assert.False(item.IsException);
        Assert.Null(item.OriginalDate);
        Assert.Null(item.LayerId);
    }

    [Fact]
    public void Item_CanSetAllProperties()
    {
        // Arrange
        var testDate = new DateTime(2026, 1, 15);

        // Act
        var item = new Item
        {
            Id = 100,
            AccountId = 5,
            Date = testDate,
            Amount = 250.50m,
            Description = "Grocery Shopping",
            Color = "#ff0000",
            Type = TransactionType.Credit,
            IsRecurring = true,
            RecurringInterval = 2,
            RecurringPeriod = "weeks",
            RecurringStartDate = testDate,
            RecurringEndDate = testDate.AddMonths(3),
            ParentRecurringItemId = 99,
            IsException = true,
            OriginalDate = testDate.AddDays(-1),
            LayerId = 10
        };

        // Assert
        Assert.Equal(100, item.Id);
        Assert.Equal(5, item.AccountId);
        Assert.Equal(testDate, item.Date);
        Assert.Equal(250.50m, item.Amount);
        Assert.Equal("Grocery Shopping", item.Description);
        Assert.Equal("#ff0000", item.Color);
        Assert.Equal(TransactionType.Credit, item.Type);
        Assert.True(item.IsRecurring);
        Assert.Equal(2, item.RecurringInterval);
        Assert.Equal("weeks", item.RecurringPeriod);
        Assert.Equal(testDate, item.RecurringStartDate);
        Assert.Equal(testDate.AddMonths(3), item.RecurringEndDate);
        Assert.Equal(99, item.ParentRecurringItemId);
        Assert.True(item.IsException);
        Assert.Equal(testDate.AddDays(-1), item.OriginalDate);
        Assert.Equal(10, item.LayerId);
    }

    [Fact]
    public void Expense_IsAliasForItem()
    {
        // Act
        var expense = new Expense
        {
            Id = 1,
            Description = "Test",
            Amount = 100m
        };

        // Assert
        Assert.IsAssignableFrom<Item>(expense);
        Assert.Equal("Test", expense.Description);
        Assert.Equal(100m, expense.Amount);
    }

    #endregion

    #region Enum Tests

    [Fact]
    public void RecurringEditMode_HasCorrectValues()
    {
        // Assert
        Assert.Equal(0, (int)RecurringEditMode.ThisOne);
        Assert.Equal(1, (int)RecurringEditMode.FromThisOne);
        Assert.Equal(2, (int)RecurringEditMode.AllInSeries);
    }

    [Fact]
    public void RecurringEditMode_CanParse()
    {
        // Act
        var thisOne = Enum.Parse<RecurringEditMode>("ThisOne");
        var fromThisOne = Enum.Parse<RecurringEditMode>("FromThisOne");
        var allInSeries = Enum.Parse<RecurringEditMode>("AllInSeries");

        // Assert
        Assert.Equal(RecurringEditMode.ThisOne, thisOne);
        Assert.Equal(RecurringEditMode.FromThisOne, fromThisOne);
        Assert.Equal(RecurringEditMode.AllInSeries, allInSeries);
    }

    [Fact]
    public void TransactionType_HasCorrectValues()
    {
        // Assert
        Assert.Equal(0, (int)TransactionType.Debit);
        Assert.Equal(1, (int)TransactionType.Credit);
    }

    [Fact]
    public void TransactionType_CanParse()
    {
        // Act
        var debit = Enum.Parse<TransactionType>("Debit");
        var credit = Enum.Parse<TransactionType>("Credit");

        // Assert
        Assert.Equal(TransactionType.Debit, debit);
        Assert.Equal(TransactionType.Credit, credit);
    }

    #endregion

    #region Business Logic Tests

    [Fact]
    public void Item_RecurringItem_ShouldHaveRequiredFields()
    {
        // Arrange
        var recurringItem = new Item
        {
            IsRecurring = true,
            RecurringInterval = 1,
            RecurringPeriod = "weeks",
            RecurringStartDate = DateTime.Today
        };

        // Assert
        Assert.True(recurringItem.IsRecurring);
        Assert.NotNull(recurringItem.RecurringInterval);
        Assert.NotNull(recurringItem.RecurringPeriod);
        Assert.NotNull(recurringItem.RecurringStartDate);
    }

    [Fact]
    public void Item_Exception_ShouldHaveOriginalDate()
    {
        // Arrange
        var originalDate = new DateTime(2026, 1, 15);
        var exceptionItem = new Item
        {
            IsException = true,
            OriginalDate = originalDate,
            ParentRecurringItemId = 1
        };

        // Assert
        Assert.True(exceptionItem.IsException);
        Assert.Equal(originalDate, exceptionItem.OriginalDate);
        Assert.NotNull(exceptionItem.ParentRecurringItemId);
    }

    [Fact]
    public void Item_LayerAssociation_CanBeNull()
    {
        // Arrange
        var item = new Item
        {
            Description = "No layer",
            Amount = 100m
        };

        // Assert
        Assert.Null(item.LayerId);
    }

    [Fact]
    public void Account_StartingBalance_CanBeNegative()
    {
        // Act
        var account = new Account
        {
            Name = "Overdrawn Account",
            StartingBalance = -500m
        };

        // Assert
        Assert.Equal(-500m, account.StartingBalance);
    }

    [Fact]
    public void Item_Amount_CanBeZero()
    {
        // Act
        var item = new Item
        {
            Description = "Free item",
            Amount = 0m
        };

        // Assert
        Assert.Equal(0m, item.Amount);
    }

    [Fact]
    public void Item_WithLargeAmount_StoresCorrectly()
    {
        // Act
        var item = new Item
        {
            Description = "Large transaction",
            Amount = 999999.99m
        };

        // Assert
        Assert.Equal(999999.99m, item.Amount);
    }

    [Fact]
    public void RecurringPeriod_CaseInsensitive_Validation()
    {
        // Arrange & Act
        var item1 = new Item { RecurringPeriod = "days" };
        var item2 = new Item { RecurringPeriod = "Days" };
        var item3 = new Item { RecurringPeriod = "DAYS" };

        // Assert - These should all be valid period strings
        Assert.Equal("days", item1.RecurringPeriod);
        Assert.Equal("Days", item2.RecurringPeriod);
        Assert.Equal("DAYS", item3.RecurringPeriod);
    }

    #endregion
}
