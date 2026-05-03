namespace JustBigO_Fun_.Services;

public interface IHintGenerator
{
    Task<string> GenerateHintAsync(string problemTitle, string problemDescription, string sourceCode, string language);
}
