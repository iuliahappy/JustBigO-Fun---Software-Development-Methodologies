using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JustBigO_Fun_.Models;

public class ProblemTest
{
    public int Id { get; set; }

    public int ProblemId { get; set; }

    [ForeignKey(nameof(ProblemId))]
    public Problem Problem { get; set; } = null!;

    /// <summary>JSON input for the test case (e.g. {"nums":[2,7,11,15],"target":9})</summary>
    [Required]
    public string InputJson { get; set; } = "{}";

    /// <summary>Expected output JSON (e.g. "[0,1]")</summary>
    [Required]
    public string ExpectedOutputJson { get; set; } = "";

    public int OrderIndex { get; set; }
}
