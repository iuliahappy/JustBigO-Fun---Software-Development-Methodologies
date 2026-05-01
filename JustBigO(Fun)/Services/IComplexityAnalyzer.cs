using System.Threading.Tasks;

namespace JustBigO_Fun_.Services
{
    public interface IComplexityAnalyzer
    {
        Task<(string TimeComplexity, string SpaceComplexity)> AnalyzeCodeAsync(string sourceCode);
    }
}