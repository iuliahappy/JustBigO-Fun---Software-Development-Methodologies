# JustBigO-Fun

JustBigO-Fun is an ASP.NET Core 9.0 MVC platform for algorithmic challenges, inspired by sites like LeetCode. It provides a full-stack environment for users to browse coding problems, submit solutions, and have them validated against test cases.

## Key Features

- **Problem Library**: Browse a collection of algorithmic challenges with difficulty levels, tags, and detailed descriptions.
- **Solution Submission**: Submit C# code for evaluation.
- **Automated Testing**: Integrated code execution system that runs submissions against predefined test cases.
- **Admin Dashboard**: Secure area for managing problems, including CRUD operations and batch uploading test cases (`.in`/`.out` files).
- **Identity & RBAC**: Complete authentication system with role-based access control for users and administrators.
- **Docker Integration**: Pluggable code execution service designed to run user code in isolated environments.

## Tech Stack

- **Backend**: .NET 9.0, ASP.NET Core MVC, C#
- **Database**: SQL Server with Entity Framework Core
- **Authentication**: ASP.NET Core Identity
- **Containerization**: Docker (for code execution)
- **Frontend**: Razor Views, Bootstrap, Vanilla CSS

## Getting Started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) (LocalDB supported)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (for the `DockerCodeExecutor`)

### Setup

1. **Clone the repository**:
   ```bash
   git clone https://github.com/your-username/JustBigO-Fun.git
   cd JustBigO-Fun
   ```

2. **Configure Database**:
   Update the connection string in `JustBigO(Fun)/appsettings.json` if you are not using the default LocalDB instance.

3. **Restore & Run**:
   ```bash
   dotnet restore
   dotnet run --project "JustBigO(Fun)"
   ```
   *Note: Database migrations and initial data seeding (Problems & Admin user) occur automatically on the first run.*

### Default Credentials

- **Admin Email**: `admin@justbigofun.local`
- **Admin Password**: `Admin123!`

## Project Structure

- `JustBigO(Fun)/Controllers/`: MVC controllers including a dedicated `Admin` area for problem management.
- `JustBigO(Fun)/Models/`: Domain entities like `Problem`, `Submission`, and `ProblemTest`.
- `JustBigO(Fun)/Services/`: Business logic, specifically the `ICodeExecutor` and `DockerCodeExecutor`.
- `JustBigO(Fun)/Data/`: EF Core context and seeders (`ProblemSeeder`, `AdminSeeder`).
- `JustBigO(Fun)/Views/`: Razor views for the public interface and administrative tools.

## Development Conventions

- **Surgical Updates**: Follow existing patterns for adding new features or fixing bugs.
- **Validation**: Use Data Annotations for model validation.
- **Security**: Decorate administrative actions with `[Authorize(Roles = AdminSeeder.AdminRole)]`.
- **Testing**: New features should be accompanied by verification logic.
