using System;
using System.IO;
using System.Text.RegularExpressions;

class Program
{
    static void Main()
    {
        string dir = @"d:\Archivos\Unity\TopDownShooter\Assets\_Project\Scripts";
        string[] files = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);
        
        using (StreamWriter sw = new StreamWriter(Path.Combine(dir, "audit_summary.txt")))
        {
            foreach (string file in files)
            {
                string content = File.ReadAllText(file);
                string relativePath = file.Substring(dir.Length + 1);
                
                // 1. Find empty Unity lifecycle methods
                var emptyMethods = Regex.Matches(content, @"void\s+(Awake|Start|Update|FixedUpdate|LateUpdate|OnEnable|OnDisable|OnDestroy)\s*\([^)]*\)\s*\{\s*\}", RegexOptions.Singleline);
                foreach (Match m in emptyMethods)
                {
                    sw.WriteLine(string.Format("[EMPTY_METHOD] {0}: {1}", relativePath, m.Groups[1].Value));
                }
                
                // 2. Find public fields (not properties, not constants, not readonly)
                var publicFields = Regex.Matches(content, @"^\s*public\s+(?!readonly|const|class|struct|enum|interface|event|delegate)[A-Za-z0-9_<>[\]]+\s+[a-z_][A-Za-z0-9_]*\s*(?:=|;)", RegexOptions.Multiline);
                foreach (Match m in publicFields)
                {
                    sw.WriteLine(string.Format("[PUBLIC_FIELD] {0}: {1}", relativePath, m.Value.Trim()));
                }
                
                // 3. Extract some comments to check language and quality
                var comments = Regex.Matches(content, @"//(.*?)$", RegexOptions.Multiline);
                int commentCount = 0;
                foreach (Match m in comments)
                {
                    string comment = m.Groups[1].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(comment) && !comment.StartsWith("=") && !comment.StartsWith("-") && comment.Length > 2)
                    {
                        if (commentCount < 10 || comment.Contains("++") || comment.Contains(";") || comment.Contains("Suma") || comment.Contains("Add")) {
                            sw.WriteLine(string.Format("[COMMENT] {0}: {1}", relativePath, comment));
                        }
                        commentCount++;
                    }
                }
                
                // 4. Extract declarations to check code language
                var decls = Regex.Matches(content, @"(?:class|struct|enum|interface)\s+([A-Za-z0-9_]+)|(?:public|private|protected|internal)\s+(?:[A-Za-z0-9_<>[\]]+\s+)?([A-Za-z0-9_]+)\s*(?:\(|;|=|{)", RegexOptions.Multiline);
                int declCount = 0;
                foreach (Match m in decls)
                {
                    string name = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
                    if (!string.IsNullOrWhiteSpace(name) && name.Length > 2)
                    {
                        if (declCount < 15) {
                            sw.WriteLine(string.Format("[DECLARATION] {0}: {1}", relativePath, name));
                        }
                        declCount++;
                    }
                }
                
                // 5. Look for commented code (very basic heuristic: ends with ; or contains { })
                var commentedCode = Regex.Matches(content, @"//\s*([A-Za-z0-9_]+\s*\([^)]*\)\s*;|.*\{.*\}|.*=.*;)", RegexOptions.Multiline);
                foreach (Match m in commentedCode)
                {
                    sw.WriteLine(string.Format("[COMMENTED_CODE] {0}: {1}", relativePath, m.Value.Trim()));
                }
            }
        }
    }
}
