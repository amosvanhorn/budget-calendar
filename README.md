# BudgetCal

BudgetCal is an ASP.NET Core MVC application designed for managing expenses and visualizing them in a calendar view. It provides a simple way to track spending, manage account balances, and plan budgets.

## Table of Contents
- [Overview](#overview)
- [Requirements](#requirements)
- [Stack](#stack)
- [Setup and Run](#setup-and-run)
- [Scripts](#scripts)
- [Environment Variables](#environment-variables)
- [Tests](#tests)
- [Project Structure](#project-structure)
- [License](#license)

## Overview
BudgetCal allows users to:
- Track expenses with details such as amount, date, and category.
- View expenses on a calendar interface.
- Manage account balances and layers of financial planning.
- Benefit from a responsive UI built with Bootstrap and jQuery.

## Requirements
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A modern web browser.
- (Optional) [JetBrains Rider](https://www.jetbrains.com/rider/) or [Visual Studio 2022+](https://visualstudio.microsoft.com/) for development.

## Stack
- **Language:** C# 14.0
- **Framework:** ASP.NET Core MVC 10.0
- **Frontend:** Razor Pages, Bootstrap, jQuery
- **Package Manager:** NuGet

## Setup and Run

### Clone the Repository
```bash
git clone https://github.com/yourusername/budget-calendar.git
cd budget-calendar
```

### Restore Dependencies
```bash
dotnet restore
```

### Build the Project
```bash
dotnet build
```

### Run the Application
```bash
dotnet run --project BudgetCal
```
By default, the application will be available at:
- HTTP: `http://localhost:5264`
- HTTPS: `https://localhost:7064`

## Scripts
Currently, there are no custom scripts (like `setup.sh` or `deploy.ps1`) in the root directory. Standard `dotnet` CLI commands are used for all lifecycle tasks.

## Environment Variables
- `ASPNETCORE_ENVIRONMENT`: Set to `Development` for development features (like Razor Runtime Compilation) or `Production` for production builds.

## Tests
- TODO: Add a test project (e.g., xUnit or NUnit) and include instructions for running tests.
- To run tests (once added):
  ```bash
  dotnet test
  ```

## Project Structure
```text
budget-calendar/
├── BudgetCal/                # Main ASP.NET Core MVC Project
│   ├── Controllers/          # MVC Controllers (e.g., ExpenseController)
│   ├── Models/               # Data models (Expense, AccountBalance, etc.)
│   ├── Views/                # Razor Views
│   ├── wwwroot/              # Static files (JS, CSS, Libs)
│   │   ├── js/               # Custom JavaScript (expense-calendar.js)
│   │   └── lib/              # Client-side libraries (Bootstrap, jQuery)
│   ├── Program.cs            # Application entry point
│   └── BudgetCal.csproj      # Project configuration and NuGet packages
├── BudgetCal.sln             # Visual Studio Solution file
└── README.md                 # This file
```

## License
- TODO: Add license information.
