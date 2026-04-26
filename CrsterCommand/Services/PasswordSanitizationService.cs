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

        // Pattern 1: Simple email/password or username/password format (e.g., admin@test.com/heloworld)
        // Match: email@domain.com/anypassword or username/password
        content = Regex.Replace(content, @"([a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,})/([^\s/]+)", 
            "$1/[REDACTED]", RegexOptions.IgnoreCase);
        content = Regex.Replace(content, @"([a-zA-Z0-9._-]+)/([^\s/]+)(?=\s|$)", 
            "$1/[REDACTED]", RegexOptions.IgnoreCase);

        // Pattern 2: Remove common password field patterns
        content = Regex.Replace(content, @"(?i)(password|pwd|passwd|pass|secret|key|token|api[_-]?key|auth[_-]?token)\s*[:=]\s*[^\s]+", 
            "$1=[REDACTED]", RegexOptions.IgnoreCase);

        // Pattern 3: URL credentials (username:password@host)
        content = Regex.Replace(content, @"([a-zA-Z0-9._%+-]+):([a-zA-Z0-9!@#$%^&*()_+]+)@", 
            "$1:[REDACTED]@", RegexOptions.IgnoreCase);

        // Pattern 4: Base64-like encoded credentials (long alphanumeric strings after key= or token=)
        content = Regex.Replace(content, @"(?i)(key|token|secret|credential)\s*[:=]\s*[A-Za-z0-9+/]{20,}={0,2}", 
            "$1=[REDACTED]", RegexOptions.IgnoreCase);

        // Pattern 5: Environment variable patterns
        content = Regex.Replace(content, @"(?i)(api_key|auth_token|secret_key|access_token|refresh_token)\s*[:=]\s*\S+", 
            "$1=[REDACTED]", RegexOptions.IgnoreCase);

        return content;
    }
}
