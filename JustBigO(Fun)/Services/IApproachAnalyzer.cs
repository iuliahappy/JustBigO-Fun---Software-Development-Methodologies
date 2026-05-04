namespace JustBigO_Fun_.Services;

/// <summary>
/// US 15: Scans partial user code and extracts a short description of the current approach / paradigm.
/// </summary>
public interface IApproachAnalyzer
{
    Task<string> DescribeApproachAsync(string partialCode, string language, CancellationToken cancellationToken = default);
}
