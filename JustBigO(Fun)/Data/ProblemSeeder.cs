using System.Text.RegularExpressions;
using JustBigO_Fun_.Models;
using Microsoft.EntityFrameworkCore;

namespace JustBigO_Fun_.Data;

public static class ProblemSeeder
{
    // Problem statements are authored in Markdown. These constants are also used to migrate
    // the original HTML-authored descriptions to Markdown on startup (see LooksLikeHtml usage below).
    private const string TwoSumDescription = """
        Given an array of integers `nums` and an integer `target`, return indices of the two numbers such that they add up to `target`.

        You may assume that each input would have **exactly one solution**, and you may not use the same element twice.

        **Input Format:** Line 1: N (number of elements). Line 2: N space-separated integers. Line 3: target integer.

        **Output Format:** Two space-separated indices.

        **Example**

        Input:
        ```text
        4
        2 7 11 15
        9
        ```
        Output:
        ```text
        0 1
        ```
        """;

    private const string LevelOrderDescription = """
        Given the `root` of a binary tree, return the level order traversal of its nodes' values.

        **Input Format:** Line 1: N (number of nodes). Line 2: N space-separated values representing the level-order traversal (use `null` for empty nodes).

        **Output Format:** Print each level on a new line, space-separated.

        **Example**

        Input:
        ```text
        7
        3 9 20 null null 15 7
        ```
        Output:
        ```text
        3
        9 20
        15 7
        ```
        """;

    private const string MinWindowDescription = """
        Given two strings `s` and `t`, return the minimum window substring of `s` such that every character in `t` (including duplicates) is included in the window.

        If there is no such substring, return the empty string.

        **Input Format:** Line 1: string s. Line 2: string t.

        **Output Format:** The substring (or empty line).

        **Example**

        Input:
        ```text
        ADOBECODEBANC
        ABC
        ```
        Output:
        ```text
        BANC
        ```
        """;

    /// <summary>Heuristic: does the description still contain raw HTML markup (legacy format)?</summary>
    private static bool LooksLikeHtml(string? text) =>
        !string.IsNullOrWhiteSpace(text) && Regex.IsMatch(text, "<[a-zA-Z][^>]*>");

    /// <summary>
    /// True if a seeded description should be refreshed to the canonical Markdown: either it is still
    /// raw HTML, or it is an earlier auto-generated form that placed Input:/Output: inside a single
    /// code fence. Admin-authored Markdown in the current format is left untouched.
    /// </summary>
    private static bool NeedsDescriptionMigration(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (LooksLikeHtml(text)) return true;
        return Regex.IsMatch(text, "```text\\s*\\r?\\nInput:");
    }

    public static async Task SeedAsync(ApplicationDbContext db)
    {
        // If problems exist, we still want to ensure MethodNames are set for this feature
        if (await db.Problems.AnyAsync())
        {
            var p1 = await db.Problems.FirstOrDefaultAsync(p => p.Slug == "two-sum");
            if (p1 != null)
            {
                if (string.IsNullOrEmpty(p1.MethodName)) p1.MethodName = "two_sum";
                p1.SignatureJson = "{\"parameters\":[{\"name\":\"nums\",\"type\":\"int[]\"},{\"name\":\"target\",\"type\":\"int\"}],\"returnType\":\"int[]\"}";
                if (NeedsDescriptionMigration(p1.Description)) p1.Description = TwoSumDescription;
            }

            var p2 = await db.Problems.FirstOrDefaultAsync(p => p.Slug == "binary-tree-level-order");
            if (p2 != null)
            {
                if (string.IsNullOrEmpty(p2.MethodName)) p2.MethodName = "level_order";
                p2.SignatureJson = "{\"parameters\":[{\"name\":\"root\",\"type\":\"TreeNode\"}],\"returnType\":\"int[][]\"}";
                if (NeedsDescriptionMigration(p2.Description)) p2.Description = LevelOrderDescription;
            }

            var p3 = await db.Problems.FirstOrDefaultAsync(p => p.Slug == "minimum-window-substring");
            if (p3 != null)
            {
                if (string.IsNullOrEmpty(p3.MethodName)) p3.MethodName = "min_window";
                p3.SignatureJson = "{\"parameters\":[{\"name\":\"s\",\"type\":\"string\"},{\"name\":\"t\",\"type\":\"string\"}],\"returnType\":\"string\"}";
                if (NeedsDescriptionMigration(p3.Description)) p3.Description = MinWindowDescription;
            }

            await db.SaveChangesAsync();
            return;
        }

        var twoSumTemplates = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["python"] = "import sys\n\ndef main():\n    # READ from stdin\n    # input: N, then N integers, then Target\n    # PRINT the two indices separated by space to stdout\n    pass\n\nif __name__ == '__main__':\n    main()",
            ["java"] = "import java.util.*;\n\npublic class Main {\n    public static void main(String[] args) {\n        Scanner sc = new Scanner(System.in);\n        // READ from stdin\n        // input: N, then N integers, then Target\n        // PRINT the two indices separated by space to stdout using System.out.println()\n    }\n}",
            ["cpp"] = "#include <iostream>\n#include <vector>\n\nusing namespace std;\n\nint main() {\n    // READ from stdin\n    // input: N, then N integers, then Target\n    // PRINT the two indices separated by space to stdout using cout\n    return 0;\n}"
        });

        var twoSum = new Problem
        {
            Title = "Two Sum",
            Slug = "two-sum",
            Difficulty = "Easy",
            Tags = "Array,Hash Map",
            OrderIndex = 1,
            MethodName = "two_sum",
            SignatureJson = "{\"parameters\":[{\"name\":\"nums\",\"type\":\"int[]\"},{\"name\":\"target\",\"type\":\"int\"}],\"returnType\":\"int[]\"}",
            Description = TwoSumDescription,
            CodeTemplatesJson = twoSumTemplates
        };

        var levelOrderTemplates = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["python"] = "import sys\n\ndef main():\n    # input: N, then N node values (or 'null')\n    # output: one line per level, values space-separated\n    pass\n\nif __name__ == '__main__':\n    main()",
            ["java"] = "import java.util.*;\n\npublic class Main {\n    public static void main(String[] args) {\n        Scanner sc = new Scanner(System.in);\n        // input: N, then N node values (or 'null')\n        // output: one line per level, values space-separated\n    }\n}",
            ["cpp"] = "#include <iostream>\n#include <vector>\n#include <string>\n\nusing namespace std;\n\nint main() {\n    // input: N, then N node values (or 'null')\n    // output: one line per level, values space-separated\n    return 0;\n}"
        });

        var levelOrder = new Problem
        {
            Title = "Binary Tree Level Order",
            Slug = "binary-tree-level-order",
            Difficulty = "Medium",
            Tags = "Tree,BFS",
            OrderIndex = 2,
            MethodName = "level_order",
            SignatureJson = "{\"parameters\":[{\"name\":\"root\",\"type\":\"TreeNode\"}],\"returnType\":\"int[][]\"}",
            Description = LevelOrderDescription,
            CodeTemplatesJson = levelOrderTemplates
        };

        var minWindowTemplates = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["python"] = "import sys\n\ndef main():\n    # input: Line 1: s, Line 2: t\n    # output: the minimum window substring\n    pass\n\nif __name__ == '__main__':\n    main()",
            ["java"] = "import java.util.*;\n\npublic class Main {\n    public static void main(String[] args) {\n        Scanner sc = new Scanner(System.in);\n        // input: Line 1: s, Line 2: t\n        // output: the minimum window substring\n    }\n}",
            ["cpp"] = "#include <iostream>\n#include <string>\n\nusing namespace std;\n\nint main() {\n    // input: Line 1: s, Line 2: t\n    // output: the minimum window substring\n    return 0;\n}"
        });

        var minWindow = new Problem
        {
            Title = "Minimum Window Substring",
            Slug = "minimum-window-substring",
            Difficulty = "Hard",
            Tags = "Sliding Window,String",
            OrderIndex = 3,
            MethodName = "min_window",
            SignatureJson = "{\"parameters\":[{\"name\":\"s\",\"type\":\"string\"},{\"name\":\"t\",\"type\":\"string\"}],\"returnType\":\"string\"}",
            Description = MinWindowDescription,
            CodeTemplatesJson = minWindowTemplates
        };

        db.Problems.AddRange(twoSum, levelOrder, minWindow);
        await db.SaveChangesAsync();

        // Add tests for Two Sum
        var twoSumId = twoSum.Id;
        if (!await db.ProblemTests.AnyAsync(t => t.ProblemId == twoSumId))
        {
            db.ProblemTests.AddRange(
                new ProblemTest { ProblemId = twoSumId, InputJson = "4\n2 7 11 15\n9", ExpectedOutputJson = "0 1", OrderIndex = 1 },
                new ProblemTest { ProblemId = twoSumId, InputJson = "3\n3 2 4\n6", ExpectedOutputJson = "1 2", OrderIndex = 2 },
                new ProblemTest { ProblemId = twoSumId, InputJson = "2\n3 3\n6", ExpectedOutputJson = "0 1", OrderIndex = 3 }
            );
        }

        // Add tests for Binary Tree Level Order
        var levelOrderId = levelOrder.Id;
        if (!await db.ProblemTests.AnyAsync(t => t.ProblemId == levelOrderId))
        {
            db.ProblemTests.AddRange(
                new ProblemTest { ProblemId = levelOrderId, InputJson = "7\n3 9 20 null null 15 7", ExpectedOutputJson = "3\n9 20\n15 7", OrderIndex = 1 },
                new ProblemTest { ProblemId = levelOrderId, InputJson = "1\n1", ExpectedOutputJson = "1", OrderIndex = 2 },
                new ProblemTest { ProblemId = levelOrderId, InputJson = "0\n", ExpectedOutputJson = "", OrderIndex = 3 }
            );
        }

        // Add tests for Minimum Window Substring
        var minWindowId = minWindow.Id;
        if (!await db.ProblemTests.AnyAsync(t => t.ProblemId == minWindowId))
        {
            db.ProblemTests.AddRange(
                new ProblemTest { ProblemId = minWindowId, InputJson = "ADOBECODEBANC\nABC", ExpectedOutputJson = "BANC", OrderIndex = 1 },
                new ProblemTest { ProblemId = minWindowId, InputJson = "a\na", ExpectedOutputJson = "a", OrderIndex = 2 },
                new ProblemTest { ProblemId = minWindowId, InputJson = "a\naa", ExpectedOutputJson = "", OrderIndex = 3 }
            );
        }

        await db.SaveChangesAsync();

    }
}
