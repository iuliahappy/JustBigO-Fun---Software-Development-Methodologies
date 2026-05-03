using System.ComponentModel.DataAnnotations;

namespace JustBigO_Fun_.Models
{
    public class SubmissionViewModel
    {
        [Required]
        public int ProblemId { get; set; }

        [Required]
        public string SourceCode { get; set; }

        public string Language { get; set; }
    }
}