namespace MyNovelBuilder.WebApi.Extensions;

/// <summary>
/// Extension methods for strings.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Sanitizes a string for use as a file name.
    /// </summary>
    /// <param name="fileName">The string to sanitize.</param>
    /// <param name="replacement">The replacement character for invalid characters.</param>
    public static string SanitizeFileName(this string fileName, string replacement = "_")
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join(replacement, fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
        return string.IsNullOrWhiteSpace(sanitized) ? "untitled" : sanitized;
    }

    /// <summary>
    /// Finds all indexes of a substring in a string.
    /// </summary>
    /// <param name="str">The string to search.</param>
    /// <param name="value">The substring to find.</param>
    /// <param name="comparison">The string comparison type to use.</param>
    /// <exception cref="ArgumentException">
    /// When the substring to find is empty.
    /// </exception>
    public static List<int> AllIndexesOf(this string str, string value, StringComparison comparison = StringComparison.Ordinal) {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException("the string to find may not be empty", nameof(value));
        }

        var occurrences = new List<int>();
        var index = 0;

        do
        {
            index = str.IndexOf(value, index, comparison);
            
            if (index != -1)
            {
                occurrences.Add(index);
                index += value.Length;
            }
        } while (index != -1);
        
        return occurrences;
    }
}