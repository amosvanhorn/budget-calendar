# 💰 BudgetCal

[![Build Status](https://img.shields.io/badge/Build-passing-brightgreen.svg)]()
[![.NET Version](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

BudgetCal is a modern ASP.NET Core MVC application designed for seamless expense management and financial visualization. Track your spending, manage account balances, and plan your budget with an intuitive calendar interface.

## 📌 Table of Contents
- [🔍 Overview](#-overview)
- [⚙️ Requirements](#️-requirements)
- [🛠️ Stack](#️-stack)
- [🚀 Setup and Run](#-setup-and-run)
- [📜 Scripts](#-scripts)
- [🌐 Environment Variables](#-environment-variables)
- [🧪 Tests](#-tests)
- [📂 Project Structure](#-project-structure)
- [📄 License](#-license)

## 🔍 Overview
BudgetCal empowers users to:
- ✅ **Track Expenses:** Detailed logging with amount, date, and category.
- 📅 **Calendar View:** Visualize your spending patterns over time.
- 🏦 **Financial Planning:** Manage account balances and multiple planning layers.
- 📱 **Responsive UI:** Built with Bootstrap and jQuery for a smooth experience across devices.

## ⚙️ Requirements
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A modern web browser (Chrome, Firefox, Edge, etc.)
- (Optional) [JetBrains Rider](https://www.jetbrains.com/rider/) or [Visual Studio 2022+](https://visualstudio.microsoft.com/)

## 🛠️ Stack
| Component | Technology |
| :--- | :--- |
| **Language** | C# 14.0 |
| **Framework** | ASP.NET Core MVC 10.0 |
| **Frontend** | Razor Pages, Bootstrap 5, jQuery |
| **Package Manager** | NuGet |

## 🚀 Setup and Run

### 📥 Clone the Repository
```bash
git clone https://github.com/yourusername/budget-calendar.git
cd budget-calendar
```

### 📦 Restore Dependencies
```bash
dotnet restore
```

### 🔨 Build the Project
```bash
dotnet build
```

### 🏁 Run the Application
```bash
dotnet run --project BudgetCal
```

By default, the application will be available at:
- 🌐 **HTTP:** `http://localhost:5264`
- 🔒 **HTTPS:** `https://localhost:7064`

## 📜 Scripts
Currently, standard `dotnet` CLI commands are used for all lifecycle tasks. No custom scripts are required.

## 🌐 Environment Variables
- `ASPNETCORE_ENVIRONMENT`: 
    - `Development`: Enables developer tools and Razor Runtime Compilation.
    - `Production`: Optimized for deployment.

## 🧪 Tests
- 📝 **TODO:** Add a test project (e.g., xUnit or NUnit).
- To run tests (once added):
  ```bash
  dotnet test
  ```

## 📂 Project Structure
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

## 📄 License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details (TODO: add LICENSE file).
