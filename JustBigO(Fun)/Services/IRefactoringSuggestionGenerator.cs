namespace JustBigO_Fun_.Services;

/// <summary>
/// US 18: Mentor compares the user's accepted solution with an optimal approach and returns refactoring guidance.
/// </summary>
public interface IRefactoringSuggestionGenerator
{
    Task<RefactoringSuggestionResult> GenerateAsync(
        string problemTitle,
        string problemDescription,
        string acceptedSourceCode,
        string language,
        CancellationToken cancellationToken = default);
}

public sealed record RefactoringSuggestionResult(
    string CodeBlockToModify,
    string OptimalDataStructure,
    string RefactoringSteps);
