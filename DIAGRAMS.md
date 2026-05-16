# System Architecture & Workflows: JustBigO-Fun

This document fulfills the requirement for Part B (Diagrams) of the MDS project.

## 1. High-Level System Architecture

The following diagram illustrates the components of the JustBigO-Fun platform and how they interact.

```mermaid
graph TD
    User([User Browser])
    ASP[ASP.NET Core MVC]
    SQL[(SQL Server)]
    Docker[Docker Sandbox]
    Ollama[Local AI - Ollama]
    SignalR[SignalR Hub]

    User <-->|HTTP/Razor| ASP
    User <-->|WebSockets| SignalR
    ASP <-->|EF Core| SQL
    ASP <-->|Process Start| Docker
    SignalR <-->|Semantic Kernel| Ollama
    ASP <-->|Semantic Kernel| Ollama
```

## 2. AI Code Translation Workflow (with Reflexion Loop)

This workflow describes how the system ensures AI-generated code is compilable before showing it to the user.

```mermaid
sequenceDiagram
    participant U as User (Frontend)
    participant H as Translation Hub
    participant A as AI Agent
    participant D as Docker Sandbox

    U->>H: Request Translation (Source, Lang)
    H->>A: Generate Initial Draft
    loop Reflexion Loop (Max 5 attempts)
        A->>D: Test Code (Compile/Run)
        D-->>A: Result (Success/Error)
        alt Success
            A-->>H: Return Validated Code
        else Error
            A->>A: Fix code using Error Message
        end
    end
    H-->>U: Final Validated Code (SignalR)
```

## 3. Submission Evaluation Workflow

```mermaid
graph LR
    Sub[Submission Created] --> Status[Status: Running]
    Status --> Compile[Prepare & Compile]
    Compile -->|Fail| CE[Compilation Error]
    Compile -->|Success| Tests[Run Test Cases]
    Tests --> Results[Aggregate Results]
    Results --> Accepted[Status: Accepted]
    Results --> WA[Status: Wrong Answer]
    Results --> TLE[Status: Time Limit Exceeded]
```
