using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using System.Text.RegularExpressions;

namespace BudgetCal.UITests;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class ExpenseCalendarTests : PageTest
{
    private const string BaseUrl = "http://localhost:5264";

    [SetUp]
    public async Task Setup()
    {
        // Ensure we start with a clean state if possible, or just go to the page
        await Page.GotoAsync(BaseUrl);
        
        // Wait for the calendar to load
        await Expect(Page.Locator("#calendarGrid")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Navigation_NextAndPreviousMonth()
    {
        var initialMonth = await Page.Locator("#monthYear").InnerTextAsync();
        Console.WriteLine($"[DEBUG_LOG] Initial month: {initialMonth}");
        
        await Page.ClickAsync("#nextMonth");
        // Wait for text to change
        await Expect(Page.Locator("#monthYear")).Not.ToHaveTextAsync(initialMonth);
        
        var nextMonth = await Page.Locator("#monthYear").InnerTextAsync();
        Console.WriteLine($"[DEBUG_LOG] Next month: {nextMonth}");
        Assert.That(nextMonth, Is.Not.EqualTo(initialMonth));

        await Page.ClickAsync("#prevMonth");
        // Wait for text to change back
        await Expect(Page.Locator("#monthYear")).ToHaveTextAsync(initialMonth);
        
        var backToInitial = await Page.Locator("#monthYear").InnerTextAsync();
        Console.WriteLine($"[DEBUG_LOG] Back to initial: {backToInitial}");
        Assert.That(backToInitial, Is.EqualTo(initialMonth));
    }

    [Test]
    public async Task Sidebar_ToggleWorks()
    {
        // Initially visible
        await Expect(Page.Locator("#layersSidebar")).ToBeVisibleAsync();
        
        // Toggle collapse
        await Page.ClickAsync("#toggleSidebarBtn");
        await Expect(Page.Locator("#layersSidebar")).Not.ToBeVisibleAsync();
        await Expect(Page.Locator("#expandSidebarBtn")).ToBeVisibleAsync();
        
        // Toggle expand
        await Page.ClickAsync("#expandSidebarBtn");
        await Expect(Page.Locator("#layersSidebar")).ToBeVisibleAsync();
        await Expect(Page.Locator("#expandSidebarBtn")).Not.ToBeVisibleAsync();
    }

    [Test]
    public async Task AccountManagement_AddAccount()
    {
        await Page.ClickAsync("#manageAccountsBtn");
        await Expect(Page.Locator("#accountModal")).ToBeVisibleAsync();
        
        var accountName = "Test Account " + Guid.NewGuid().ToString().Substring(0, 5);
        await Page.FillAsync("#accountName", accountName);
        await Page.FillAsync("#accountStartBalance", "5000");
        await Page.FillAsync("#accountStartDate", "2025-01-01");
        
        await Page.ClickAsync("#saveAccountBtn");
        
        // Check if account is in the selector
        // Using all().ToContainTextAsync to avoid strict mode violation if we just want to check if ANY option has the text
        await Expect(Page.Locator("#accountSelector option")).ToContainTextAsync(new[] { accountName });
    }

    [Test]
    public async Task LayerManagement_AddLayer()
    {
        await Page.ClickAsync("#addLayerBtn");
        await Expect(Page.Locator("#layerModal")).ToBeVisibleAsync();
        
        var layerName = "Test Layer " + Guid.NewGuid().ToString().Substring(0, 5);
        await Page.FillAsync("#layerName", layerName);
        await Page.ClickAsync("#saveLayerBtn");
        
        // Check if layer is in the list
        await Expect(Page.Locator("#layersList")).ToContainTextAsync(layerName);
    }

    [Test]
    public async Task ItemManagement_AddExpense()
    {
        // Click on a day to open modal (e.g., first day that has a number)
        var dayNumber = Page.Locator(".calendar-cell:not(.empty) .day-number").First;
        await dayNumber.ClickAsync();
        
        await Expect(Page.Locator("#expenseModal")).ToBeVisibleAsync();
        
        var expenseName = "Lunch " + Guid.NewGuid().ToString().Substring(0, 5);
        await Page.FillAsync("#expenseDescription", expenseName);
        await Page.FillAsync("#expenseAmount", "15.50");
        await Page.SelectOptionAsync("#expenseType", "Debit");
        
        await Page.ClickAsync("button:has-text('SAVE')");
        
        // Verify it appeared on the calendar
        await Expect(Page.Locator("#calendarGrid")).ToContainTextAsync(expenseName);
        await Expect(Page.Locator("#calendarGrid")).ToContainTextAsync("15.50");
    }

    [Test]
    public async Task ItemManagement_AddRecurringIncome()
    {
        var dayNumber = Page.Locator(".calendar-cell:not(.empty) .day-number").First;
        await dayNumber.ClickAsync();
        
        await Expect(Page.Locator("#expenseModal")).ToBeVisibleAsync();

        var incomeName = "Salary " + Guid.NewGuid().ToString().Substring(0, 5);
        await Page.FillAsync("#expenseDescription", incomeName);
        await Page.FillAsync("#expenseAmount", "3000");
        await Page.SelectOptionAsync("#expenseType", "Credit");
        
        // Enable recurring
        await Page.ClickAsync("label:has(#isRecurring)");
        await Expect(Page.Locator("#recurringOptionsInline")).ToBeVisibleAsync();
        
        await Page.FillAsync("#recurringInterval", "1");
        await Page.SelectOptionAsync("#recurringPeriod", "months");
        
        await Page.ClickAsync("button:has-text('SAVE')");
        
        // Verify it appeared
        await Expect(Page.Locator("#calendarGrid")).ToContainTextAsync(incomeName);
        
        // Navigate to next month and verify it's there too
        var initialMonth = await Page.Locator("#monthYear").InnerTextAsync();
        await Page.ClickAsync("#nextMonth");
        await Expect(Page.Locator("#monthYear")).Not.ToHaveTextAsync(initialMonth);

        await Expect(Page.Locator("#calendarGrid")).ToContainTextAsync(incomeName);
    }

    [Test]
    public async Task BalanceOverride_SetBalance()
    {
        // Find a day and right-click to open balance modal
        // In this app, balance override might be triggered by clicking on the balance display in a cell
        var balanceDisplay = Page.Locator(".day-balance").First;
        await balanceDisplay.ClickAsync();
        
        await Expect(Page.Locator("#balanceModal")).ToBeVisibleAsync();
        
        await Page.FillAsync("#balanceAmount", "1234.56");
        await Page.ClickAsync("button:has-text('SET BALANCE')");
        
        // Check if the balance updated and shows as override (usually a different class or bold)
        await Expect(balanceDisplay).ToHaveTextAsync("$1234.56");
        await Expect(balanceDisplay).ToHaveClassAsync(new Regex("override"));
    }

    [Test]
    public async Task Modal_CancelAndClose()
    {
        // Open Expense Modal
        await Page.Locator(".calendar-cell:not(.empty) .day-number").First.ClickAsync();
        await Expect(Page.Locator("#expenseModal")).ToBeVisibleAsync();
        
        // Click Cancel
        await Page.ClickAsync("#cancelExpenseBtn");
        await Expect(Page.Locator("#expenseModal")).Not.ToBeVisibleAsync();
        
        // Open again
        await Page.Locator(".calendar-cell:not(.empty) .day-number").First.ClickAsync();
        await Expect(Page.Locator("#expenseModal")).ToBeVisibleAsync();
        // Click X
        await Page.ClickAsync("#expenseModal .close");
        await Expect(Page.Locator("#expenseModal")).Not.ToBeVisibleAsync();
    }
}
