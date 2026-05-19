# JustBigO-Fun

JustBigO-Fun is an ASP.NET Core 9.0 MVC platform for algorithmic challenges, inspired by sites like LeetCode. It provides a full-stack environment for users to browse coding problems, submit solutions, and have them validated against test cases. 

## Key Features

- **Problem Library**: Browse a collection of algorithmic challenges with difficulty levels, tags, and detailed descriptions.
- **Solution Submission**: Submit C#, Python, Java, and C++ code for evaluation.
- **AI Transpiler & Mentor**: Features an integrated local AI Agent (Llama 3.2) that can seamlessly translate code between languages and provide hints. The AI uses an autonomous "Reflexion" loop to pre-compile and fix its own code before displaying it.
- **Automated Testing**: Integrated Docker-based code execution sandbox that runs user submissions and AI drafts against predefined test cases in isolated environments.
- **Admin Dashboard**: Secure area for managing problems, including CRUD operations and batch uploading test cases (`.in`/`.out` files).
- **Identity & RBAC**: Complete authentication system with role-based access control for users and administrators.

## Tech Stack

- **Backend**: .NET 9.0, ASP.NET Core MVC, C#, SignalR
- **Database**: SQL Server with Entity Framework Core
- **Authentication**: ASP.NET Core Identity
- **Containerization**: Docker (Code Execution Sandbox)
- **Local AI Engine**: Semantic Kernel & Ollama (Llama 3.2)
- **Frontend**: Razor Views, Bootstrap, Vanilla CSS, Monaco Editor

## Getting Started

### Prerequisites

To run this project locally, you must have the following installed and running:
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) (LocalDB supported)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (**Must be running** in the background)
- [Ollama](https://ollama.com/) (**Must be installed** for the local AI agent)

### Setup Instructions

1. **Clone the repository**:
   ```bash
   git clone [https://github.com/your-username/JustBigO-Fun.git](https://github.com/your-username/JustBigO-Fun.git)
   cd JustBigO-Fun
   ```
   

2. **Build the Docker Execution Sandbox**:
   The application requires a specific Docker image to compile user and AI code. Ensure Docker Desktop is running, then execute this command in the project root:
   ```bash
   docker build -f Runner.Dockerfile -t justbigo-runner:latest .
   ```

3. **Download the AI Model**:
   The AI transpilation engine relies on the Llama 3.2 model. Run this command to download it to your local machine (this may take a few minutes depending on your internet connection):
   ```bash
   ollama run llama3.2
   ```
   *(Note: You can close the Ollama chat prompt once the download finishes, but the Ollama app must remain running in your system tray).*

4. **Configure Database**:
   Update the connection string in `JustBigO(Fun)/appsettings.json` if you are not using the default LocalDB instance.

5. **Restore & Run**:
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
- `JustBigO(Fun)/Hubs/`: SignalR hubs for real-time AI code streaming.
- `JustBigO(Fun)/Models/`: Domain entities like `Problem`, `Submission`, and `ProblemTest`.
- `JustBigO(Fun)/Services/`: Business logic, specifically the `ICodeExecutor` (Docker Sandbox) and `ICodeTranslatorAgent` (AI).
- `JustBigO(Fun)/Data/`: EF Core context and seeders (`ProblemSeeder`, `AdminSeeder`).
- `JustBigO(Fun)/Views/`: Razor views for the public interface and administrative tools.

## Development Conventions

- **Surgical Updates**: Follow existing patterns for adding new features or fixing bugs.
- **Validation**: Use Data Annotations for model validation.
- **Security**: Decorate administrative actions with `[Authorize(Roles = AdminSeeder.AdminRole)]`.
- **Testing**: New features should be accompanied by verification logic.
