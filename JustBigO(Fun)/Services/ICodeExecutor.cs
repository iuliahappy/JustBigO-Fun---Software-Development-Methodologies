using JustBigO_Fun_.Models;

namespace JustBigO_Fun_.Services;

public interface ICodeExecutor
{
    Task ExecuteAsync(int submissionId);
}
