# Report on the Use of AI Tools in Software Development

**Project:** JustBigO-Fun — platform for algorithmic challenges (ASP.NET Core 9.0 MVC)
**Course:** MDS — Component B, item "Report on the use of AI tools during software development" (2 pts)
**Date:** June 2026

> Terminology note: AI appears on **two distinct planes** in this project, which we treat separately:
> 1. **AI as part of the product** — the agents integrated into the application (Mentor / Transpiler on Llama 3.2 via Ollama + Semantic Kernel, plus the Gemini-based generators). These are functionalities, not code-writing tools. 
> 2. **AI as a development tool** — the tools the team used to *build* the project (Gemini web, Cursor, Gemini CLI, Claude Code). **This report focuses mainly on plane 2**, as required.

---

## 1. Team and AI Tools Used

| Member | AI development tools | Primary way of working |
|--------|----------------------|------------------------|
| Bâcă Ionuț-Adelin | **Gemini (web)** | Conversational in the browser: snippet generation, explanations, copy-paste debugging. |
| Ștefan Rotaru | **Gemini (web)** | Conversational in the browser: writing problem descriptions, explanations, and code fragments. |
| Popescu Iulia-Maria | **Gemini (web) + Cursor** | Gemini for exploration/questions, Cursor as an AI-integrated IDE (context-aware autocomplete, inline editing, chat over files). |
| Dumitrescu Mădălina-Camelia | **Gemini CLI**, and in the final stages **Claude Code** | Agentic in the terminal, with direct access to the repo files; transition to Claude Code for the complex end-of-project tasks. |

**Note on the evolution:** the team started with **conversational** tools (Gemini web — copy-paste answers, no code access) and gradually migrated to **agentic** tools integrated into the workflow (Cursor in the IDE; Gemini CLI and Claude Code in the terminal, with direct file access, command execution, and multi-file editing). This transition reduced the "copy-paste" overhead and improved accuracy, because the agentic tools could see the project's real context.

---

## 2. Profile of Each Tool, as Used in the Project

### Gemini (web)
- **Used by the entire team.**
- **Strengths in practice:** instant access, no setup; good for conceptual explanations, writing problem descriptions, and generating isolated fragments.
- **Limitations encountered:** does not "see" the codebase → answers that don't match the real structure; requires manual copy-paste and adaptation; easy to lose context between messages.

### Cursor
- **Used by:** Popescu Iulia-Maria.
- **Strengths:** file-context autocomplete, inline editing, chat that "sees" the open files. Sped up writing Razor Views and UI logic.
- **Limitations:** suggestions on large files needed manual review; occasionally proposed patterns that diverged from the project's conventions.

### Gemini CLI
- **Used by:** Dumitrescu Mădălina-Camelia.
- **Strengths:** agentic in the terminal, with access to files and commands — suited for repetitive tasks and integration with the git workflow. The `GEMINI.md` file in the repo served as persistent context/instructions for the agent.
- **Limitations:** on very complex tasks (large refactors, resolving merge conflicts) it needed step-by-step guidance.

### Claude Code
- **Used by:** Dumitrescu Mădălina-Camelia, in the **final stages**.
- **Strengths:** strong on multi-file, high-complexity tasks at the end of the project — the Admin area overhaul (problem editor with Markdown + Monaco, test CRUD, auto-ordering), the admin dashboard with user/role management, and the stabilization of tests and timeout flows. It worked directly on the repo (reading, editing, running commands, git).
- **Limitations:** usage limits (we reserved it for complex tasks); tends to over-engineer, so it had to be constrained to "surgical" changes; architectural decisions and generated code require human verification (running tests/app) before merge.

---

## 3. Use of AI Across Each Phase of the Process (mapped to the B rubric)

### 3.1 User stories & backlog (2 pts)
- User stories were **brainstormed and refined with Gemini (web)** by Dumitrescu Mădălina-Camelia.
- AI was used to rephrase them into the standard format ("As a user, I want… so that…") and to identify acceptance criteria.

### 3.2 Diagrams (1 pt)
- `DIAGRAMS.md` (architecture / workflow / component diagrams) was generated and clarified with AI assistance — see commit `f03c9e3 docs: improve diagrams clarity`.
- Gemini helped translate the flows (e.g., the Reflexion loop, the sandbox execution flow) into text/Mermaid diagrams.

### 3.3 Source control with git (1 pt)
- The feature-branch flow (`feature/generic-executor-metrics`, `fix/admin-area-overhaul`, `Transpilare`, `Indicii_US12_US13`, etc.), merges, and **pull requests** (#4–#19) was supported by AI for:
  - drafting commit messages and PR descriptions;
  - **resolving merge conflicts** (e.g., `9e0cfae`, `f76e98d` — conflicts in `Solve.cshtml` and `ICodeExecutor.cs` resolved with AI assistance, keeping both features).
- Gemini CLI and Claude Code could run git commands directly, easing rebases and integration.

### 3.4 Automated tests, including agent evals (2 pts)
- The suite in `JustBigO(Fun).Tests/` was generated and refined with AI:
  - **Controllers:** `HomeControllerTests`, `SubmissionControllerTests`;
  - **Models:** `ProblemTests`;
  - **Hubs:** `TranslationHubTests` (SignalR streaming);
  - **Services:** `DockerCodeExecutorTests` (TLE/OOM cases), `AgentComplexityAnalyzerTests`, `CurrentCodeCompletionServiceTests`.
- **Agent evals** (an explicit requirement): `AI/CodeTranslatorAgentTests.cs`, `GeminiHintGeneratorTests.cs`, `GeminiRefactoringSuggestionGeneratorTests.cs` — these check the structural integrity of AI-generated responses (not just "classic" code). See also commits `fa0c206` and `fac9b74`.

### 3.5 Bug reporting and resolution via pull request (1 pt)
- Real bugs identified and fixed via PR, with AI assistance:
  - "No redirect to login page" and "Grey text on dark background" → `52d065b`, PR #17 (`ui-fixes`);
  - fixing tests after the resource-limit changes → `bc794ff`;
  - stopping the AI query after a timeout (stability) → `bda94f0`, `452db2b`.
- AI was used both to **diagnose** the cause and to propose the fix and draft the PR.

### 3.6 CI/CD pipeline (1 pt)
- `.github/workflows/dotnet-ci.yml` was **generated with AI** based on the project structure (build + run tests on .NET 9). See commit `fac9b74`, which introduces the CI/CD pipeline together with the automated tests.

### 3.7 Implementation (as much working AI-written code as possible)
Significant AI-assisted/generated code contributions:
- **The "Reflexion" loop** — the agent re-reads its own compiler errors and fixes its code before displaying it (co-authored with AI).
- **Multi-language transpiler** (C# / Python / Java / C++) and the completion/hints feature.
- **SignalR integration** for real-time streaming of AI responses.
- **Docker sandbox** (`DockerCodeExecutor`, `Runner.Dockerfile`) for isolated execution, handling TLE/OOM.
- **Admin area overhaul** (problem editor with Markdown + Monaco, test CRUD, auto-ordering) and the **admin dashboard** with RBAC — built in the final stages with **Claude Code**.

---

## 4. Comparative Reflections and Lessons Learned

1. **Conversational vs. agentic.** Gemini web was excellent for exploration and learning, but costly in time (copy-paste, re-adaptation). The agentic tools (Cursor, Gemini CLI, Claude Code), having code access, produced more correct and faster changes on the real codebase.
2. **Matching tool to task.** For isolated, conceptual tasks → Gemini web. For in-IDE editing → Cursor. For terminal and git automation → Gemini CLI. For complex, multi-file, end-of-project tasks → Claude Code.
3. **Human oversight remained critical**, especially for:
   - **Safety:** properly isolating the Docker sandbox.
   - **Logical correctness:** the AI initially ignored cancellation tokens, which was caught and fixed to allow real timeouts.
   - **Prompt engineering:** tuning the "absolute laws" in the system prompts so the agent gives **hints**, not full solutions, when only a hint is requested.
4. **Adoption curve.** Moving from Gemini web to agentic tools was the single biggest source of productivity gains in the second half of the project.

---

## 5. Conclusion

AI was present across **all phases** of the development lifecycle — from user stories and diagrams, to implementation, tests/evals, bug fixing via PR, and CI/CD. The team used a complementary mix of tools (Gemini web, Cursor, Gemini CLI, Claude Code), evolving from conversational assistance toward agentic tools integrated into the workflow. The result is a feature-rich platform delivered in considerably less time, where AI served both as a **development tool** and as an **architectural component** of the final product (the Mentor/Transpiler agents in the application).
