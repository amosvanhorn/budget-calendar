using BudgetCal.Controllers;
using BudgetCal.Models;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace BudgetCal.Tests;

public class HomeControllerTests
{
    [Fact]
    public void Index_ReturnsViewResult()
    {
        // Arrange
        var controller = new HomeController();

        // Act
        var result = controller.Index();

        // Assert
        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void Error_ReturnsViewResult_WithErrorViewModel()
    {
        // Arrange
        var controller = new HomeController();

        // Act
        var result = controller.Error();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.IsType<ErrorViewModel>(viewResult.Model);
    }

    [Fact]
    public void Error_ViewModel_HasRequestId()
    {
        // Arrange
        var controller = new HomeController();

        // Act
        var result = controller.Error();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ErrorViewModel>(viewResult.Model);
        Assert.NotNull(model.RequestId);
        Assert.NotEmpty(model.RequestId);
    }
}
