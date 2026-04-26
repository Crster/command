using System.Text.RegularExpressions;

namespace CrsterCommand.Services;

public static class PasswordSanitizationService
{
    /// <summary>
    /// Removes password patterns from content for safe display and AI processing.
    /// Handles various credential formats while preserving context.
    /// </summary>
    public static string RemovePasswordPatterns(string content)
    {
        if (string.IsNullOrEmpty(content)) return content;

        // Pattern 1: SSH/PEM private key blocks
        content = Regex.Replace(content, @"-----BEGIN [A-Z ]*PRIVATE KEY-----[\s\S]*?-----END [A-Z ]*PRIVATE KEY-----",
            "[PASSWORD]", RegexOptions.IgnoreCase);

        // Pattern 2: URL credentials (username:password@host) — run before slash patterns
        content = Regex.Replace(content, @"([a-zA-Z0-9._%+-]+):([a-zA-Z0-9!@#$%^&*()_+\-]+)@",
            "$1:[PASSWORD]@", RegexOptions.IgnoreCase);

        // Pattern 3: email/password slash format (e.g., admin@test.com/mypassword)
        content = Regex.Replace(content, @"([a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,})/([^\s/]+)",
            "$1/[PASSWORD]", RegexOptions.IgnoreCase);

        // Pattern 4: username/password slash format
        content = Regex.Replace(content, @"([a-zA-Z0-9._-]+)/([^\s/]+)(?=\s|$)",
            "$1/[PASSWORD]", RegexOptions.IgnoreCase);

        // Pattern 5: Authorization / Bearer headers
        content = Regex.Replace(content, @"(?i)(Authorization\s*:\s*Bearer\s+)\S+",
            "$1[PASSWORD]");
        content = Regex.Replace(content, @"(?i)(Bearer\s+)[A-Za-z0-9\-._~+/]+=*",
            "$1[PASSWORD]");

        // Pattern 6: JSON/YAML quoted key-value password fields
        content = Regex.Replace(content, @"(?i)(""(?:password|passwd|pwd|secret|token|api_key|private_key|client_secret|access_token|refresh_token|auth_token|passphrase)""\s*:\s*)(""[^""]*""|'[^']*')",
            "$1\"[PASSWORD]\"");

        // Pattern 7: YAML unquoted key-value password fields (password: value)
        content = Regex.Replace(content, @"(?i)^(\s*(?:password|passwd|pwd|secret|token|api_key|private_key|client_secret|access_token|refresh_token|auth_token|passphrase|pin)\s*:\s*)(?!\[PASSWORD\])\S+",
            "$1[PASSWORD]", RegexOptions.Multiline);

        // Pattern 8: Common password field labels with = or : separator
        content = Regex.Replace(content, @"(?i)(password|pwd|passwd|pass|passphrase|secret|pin|token|api[_-]?key|auth[_-]?token|private[_-]?key|master[_-]?key|encryption[_-]?key)\s*[:=]\s*\S+",
            "$1=[PASSWORD]");

        // Pattern 9: Environment variable patterns (KEY=VALUE on its own line or inline)
        content = Regex.Replace(content, @"(?i)(api_key|auth_token|secret_key|access_token|refresh_token|client_secret|client_id|bearer_token|session_token|csrf_token|jwt|jwt_token|db_password|database_password)\s*[:=]\s*\S+",
            "$1=[PASSWORD]");

        // Pattern 10: Base64-like encoded secrets (20+ char alphanumeric after credential keyword)
        content = Regex.Replace(content, @"(?i)(key|token|secret|credential|hash|salt)\s*[:=]\s*[A-Za-z0-9+/=_\-]{20,}",
            "$1=[PASSWORD]");

        // Pattern 11: Connection strings with Password= or Pwd= segments
        content = Regex.Replace(content, @"(?i)(Password|Pwd)\s*=\s*[^;""'\s]+",
            "$1=[PASSWORD]");

        // Pattern 12: Windows INI / registry style on its own line (Password = value)
        content = Regex.Replace(content, @"(?im)^(Password|Pwd|Pass)\s*=\s*.+$",
            "$1=[PASSWORD]");

        // Pattern 13: Credit card numbers (4×4 digit groups)
        content = Regex.Replace(content, @"\b(\d{4}[\s\-]?\d{4}[\s\-]?\d{4}[\s\-]?\d{4})\b",
            "[PASSWORD]");

        // Pattern 14: Social Security / national ID numbers (###-##-####)
        content = Regex.Replace(content, @"\b\d{3}-\d{2}-\d{4}\b",
            "[PASSWORD]");

        // Pattern 15: Generic high-entropy standalone tokens (32+ hex chars, e.g., MD5/SHA hashes, UUIDs without dashes)
        content = Regex.Replace(content, @"\b[a-fA-F0-9]{32,}\b",
            "[PASSWORD]");

        return content;
    }
}
