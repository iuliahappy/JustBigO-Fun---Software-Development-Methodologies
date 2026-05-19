using JustBigO_Fun_.Models;
using Microsoft.EntityFrameworkCore;

namespace JustBigO_Fun_.Data;

public static class ProblemSeeder
{
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
            }

            var p2 = await db.Problems.FirstOrDefaultAsync(p => p.Slug == "binary-tree-level-order");
            if (p2 != null)
            {
                if (string.IsNullOrEmpty(p2.MethodName)) p2.MethodName = "level_order";
                p2.SignatureJson = "{\"parameters\":[{\"name\":\"root\",\"type\":\"TreeNode\"}],\"returnType\":\"int[][]\"}";
            }

            var p3 = await db.Problems.FirstOrDefaultAsync(p => p.Slug == "minimum-window-substring");
            if (p3 != null)
            {
                if (string.IsNullOrEmpty(p3.MethodName)) p3.MethodName = "min_window";
                p3.SignatureJson = "{\"parameters\":[{\"name\":\"s\",\"type\":\"string\"},{\"name\":\"t\",\"type\":\"string\"}],\"returnType\":\"string\"}";
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
            Description = """
                <p>Given an array of integers <code>nums</code> and an integer <code>target</code>, return indices of the two numbers such that they add up to <code>target</code>.</p>
                <p>You may assume that each input would have <strong>exactly one solution</strong>, and you may not use the same element twice.</p>
                <p><strong>Input Format:</strong> Line 1: N (number of elements). Line 2: N space-separated integers. Line 3: target integer.</p>
                <p><strong>Output Format:</strong> Two space-separated indices.</p>
                <div class="jbo-example-box">
                    <strong>Input:</strong><br/>4<br/>2 7 11 15<br/>9<br />
                    <strong>Output:</strong> 0 1
                </div>
                """,
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
            Description = """
                <p>Given the <code>root</code> of a binary tree, return the level order traversal of its nodes' values.</p>
                <p><strong>Input Format:</strong> Line 1: N (number of nodes). Line 2: N space-separated values representing the level-order traversal (use 'null' for empty nodes).</p>
                <p><strong>Output Format:</strong> Print each level on a new line, space-separated.</p>
                <div class="jbo-example-box">
                    <strong>Input:</strong><br/>7<br/>3 9 20 null null 15 7<br />
                    <strong>Output:</strong><br/>3<br/>9 20<br/>15 7
                </div>
                """,
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
            Description = """
                <p>Given two strings <code>s</code> and <code>t</code>, return the minimum window substring of <code>s</code> such that every character in <code>t</code> (including duplicates) is included in the window.</p>
                <p>If there is no such substring, return the empty string.</p>
                <p><strong>Input Format:</strong> Line 1: string s. Line 2: string t.</p>
                <p><strong>Output Format:</strong> The substring (or empty line).</p>
                <div class="jbo-example-box">
                    <strong>Input:</strong><br/>ADOBECODEBANC<br/>ABC<br />
                    <strong>Output:</strong> BANC
                </div>
                """,
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
