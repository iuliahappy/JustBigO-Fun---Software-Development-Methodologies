using JustBigO_Fun_.Models;
using Microsoft.EntityFrameworkCore;

namespace JustBigO_Fun_.Data;

public static class ProblemSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db)
    {
        if (await db.Problems.AnyAsync())
            return;

        var twoSumTemplates = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["python"] = string.Join("\n", [
                "def two_sum(nums, target):",
                "    # Write your solution here",
                "    pass"
            ]),
            ["java"] = string.Join("\n", [
                "class Solution {",
                "    public int[] twoSum(int[] nums, int target) {",
                "        // Write your solution here",
                "        return new int[]{};",
                "    }",
                "}"
            ]),
            ["cpp"] = string.Join("\n", [
                "#include <vector>",
                "",
                "class Solution {",
                "public:",
                "    std::vector<int> twoSum(std::vector<int>& nums, int target) {",
                "        // Write your solution here",
                "        return {};",
                "    }",
                "};"
            ])
        });

        var twoSum = new Problem
        {
            Title = "Two Sum",
            Slug = "two-sum",
            Difficulty = "Easy",
            Tags = "Array,Hash Map",
            OrderIndex = 1,
            Description = """
                <p>Given an array of integers <code>nums</code> and an integer <code>target</code>, return indices of the two numbers such that they add up to <code>target</code>.</p>
                <p>You may assume that each input would have <strong>exactly one solution</strong>, and you may not use the same element twice.</p>
                <div class="jbo-example-box">
                    <strong>Input:</strong> nums = [2,7,11,15], target = 9<br />
                    <strong>Output:</strong> [0,1]<br />
                    <strong>Explanation:</strong> Because nums[0] + nums[1] == 9, we return [0, 1].
                </div>
                """,
            CodeTemplatesJson = twoSumTemplates
        };

        var levelOrderTemplates = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["python"] = string.Join("\n", [
                "from collections import deque",
                "",
                "def level_order(root):",
                "    # Write your solution here",
                "    pass"
            ]),
            ["java"] = string.Join("\n", [
                "import java.util.*;",
                "",
                "class Solution {",
                "    public List<List<Integer>> levelOrder(TreeNode root) {",
                "        // Write your solution here",
                "        return new ArrayList<>();",
                "    }",
                "}"
            ]),
            ["cpp"] = string.Join("\n", [
                "#include <vector>",
                "#include <queue>",
                "",
                "class Solution {",
                "public:",
                "    std::vector<std::vector<int>> levelOrder(TreeNode* root) {",
                "        // Write your solution here",
                "        return {};",
                "    }",
                "};"
            ])
        });

        var levelOrder = new Problem
        {
            Title = "Binary Tree Level Order",
            Slug = "binary-tree-level-order",
            Difficulty = "Medium",
            Tags = "Tree,BFS",
            OrderIndex = 2,
            Description = """
                <p>Given the <code>root</code> of a binary tree, return the level order traversal of its nodes' values.</p>
                <p>Return the result as a list of lists, where each inner list contains the values of nodes at that level, from left to right.</p>
                <div class="jbo-example-box">
                    <strong>Input:</strong> root = [3,9,20,null,null,15,7]<br />
                    <strong>Output:</strong> [[3],[9,20],[15,7]]
                </div>
                """,
            CodeTemplatesJson = levelOrderTemplates
        };

        var minWindowTemplates = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["python"] = string.Join("\n", [
                "def min_window(s, t):",
                "    # Write your solution here",
                "    pass"
            ]),
            ["java"] = string.Join("\n", [
                "class Solution {",
                "    public String minWindow(String s, String t) {",
                "        // Write your solution here",
                "        return \"\";",
                "    }",
                "}"
            ]),
            ["cpp"] = string.Join("\n", [
                "#include <string>",
                "",
                "class Solution {",
                "public:",
                "    std::string minWindow(std::string s, std::string t) {",
                "        // Write your solution here",
                "        return \"\";",
                "    }",
                "};"
            ])
        });

        var minWindow = new Problem
        {
            Title = "Minimum Window Substring",
            Slug = "minimum-window-substring",
            Difficulty = "Hard",
            Tags = "Sliding Window,String",
            OrderIndex = 3,
            Description = """
                <p>Given two strings <code>s</code> and <code>t</code>, return the minimum window substring of <code>s</code> such that every character in <code>t</code> (including duplicates) is included in the window.</p>
                <p>If there is no such substring, return the empty string <code>""</code>.</p>
                <div class="jbo-example-box">
                    <strong>Input:</strong> s = "ADOBECODEBANC", t = "ABC"<br />
                    <strong>Output:</strong> "BANC"
                </div>
                """,
            CodeTemplatesJson = minWindowTemplates
        };

        db.Problems.AddRange(twoSum, levelOrder, minWindow);
        await db.SaveChangesAsync();

        // Add tests for Two Sum
        var twoSumId = twoSum.Id;
        db.ProblemTests.AddRange(
            new ProblemTest { ProblemId = twoSumId, InputJson = """{"nums":[2,7,11,15],"target":9}""", ExpectedOutputJson = "[0,1]", OrderIndex = 1 },
            new ProblemTest { ProblemId = twoSumId, InputJson = """{"nums":[3,2,4],"target":6}""", ExpectedOutputJson = "[1,2]", OrderIndex = 2 },
            new ProblemTest { ProblemId = twoSumId, InputJson = """{"nums":[3,3],"target":6}""", ExpectedOutputJson = "[0,1]", OrderIndex = 3 }
        );
        await db.SaveChangesAsync();
    }
}
