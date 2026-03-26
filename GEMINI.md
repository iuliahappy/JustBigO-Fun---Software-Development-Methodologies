# JustBigO-Fun

JustBigO-Fun is an ASP.NET Core 9.0 MVC web application designed as a platform for algorithmic challenges, similar to platforms like LeetCode. It allows users to browse problems, view detailed descriptions, and manage test cases.

## Project Overview

- **Purpose**: A practice platform for software development methodologies and algorithmic problem solving.
- **Target Framework**: .NET 9.0
- **Primary Technologies**:
  - **Backend**: ASP.NET Core MVC, C#
  - **Database**: SQL Server with Entity Framework Core
  - **Security**: ASP.NET Core Identity with Role-based Access Control (RBAC)
  - **Frontend**: Razor Views, CSS, Bootstrap

## Architecture & Structure

The project follows a standard ASP.NET Core MVC architectural pattern:

- **Models (`JustBigO(Fun)/Models/`)**:
  - `Problem`: Core entity representing a coding challenge (Title, Slug, Description, Difficulty, Tags, Code Templates).
  - `ProblemTest`: Represents a test case for a problem (Input/Output JSON).
- **Controllers (`JustBigO(Fun)/Controllers/`)**:
  - `HomeController`: Manages the public-facing problem list and solving interface.
  - `Admin/ProblemsController`: Located in the `Admin` area, handles CRUD operations for problems and test case uploads.
- **Data (`JustBigO(Fun)/Data/`)**:
  - `ApplicationDbContext`: EF Core context for SQL Server.
  - `ProblemSeeder`: Automatically populates the database with sample problems (Two Sum, Binary Tree Level Order, etc.).
  - `AdminSeeder`: Sets up default roles and an administrator account (`admin@justbigofun.local`).
- **Areas**:
  - `Admin`: Restricted area for problem management.
  - `Identity`: Default ASP.NET Core Identity pages for authentication.

## Building and Running

### Prerequisites
- .NET 9 SDK
- SQL Server (LocalDB or full instance)

### Setup & Run
1.  **Configure Database**: Update the connection string in `JustBigO(Fun)/appsettings.json` if necessary.
    ```json
    "ConnectionStrings": {
      "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=aspnet-JustBigO_Fun_-...;Trusted_Connection=True;MultipleActiveResultSets=true"
    }
    ```
2.  **Restore and Run**:
    ```bash
    dotnet restore
    dotnet run --project JustBigO(Fun)
    ```
    *Note: Migrations and data seeding (Problems & Admin user) occur automatically on application startup.*

### Default Credentials
- **Admin Email**: `admin@justbigofun.local`
- **Admin Password**: `Admin123!`

## Development Conventions

- **Models & Validation**: Use `System.ComponentModel.DataAnnotations` for model validation.
- **ViewModels**: Always use ViewModels (suffix `Vm` or `ViewModel`) when passing data to Views to avoid over-posting and keep views clean.
- **Authentication**: Admin controllers must be decorated with `[Authorize(Roles = AdminSeeder.AdminRole)]` and use the `Admin` Area.
- **Routing**: Slug-based routing is preferred for problems (e.g., `/problems/two-sum`).
- **Test Cases**: Test inputs and outputs are stored as JSON strings in the database. The Admin panel supports batch uploading via `.in` and `.out` files.

## Key Files
- `JustBigO(Fun)/Program.cs`: Application entry point, service registration, and middleware pipeline.
- `JustBigO(Fun)/Models/Problem.cs`: Main domain model for coding challenges.
- `JustBigO(Fun)/Data/ProblemSeeder.cs`: Contains the logic for initial problem data.
- `JustBigO(Fun)/Controllers/Admin/ProblemsController.cs`: Core logic for managing the problem library.
