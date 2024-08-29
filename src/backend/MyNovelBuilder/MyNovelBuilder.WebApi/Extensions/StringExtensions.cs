namespace MyNovelBuilder.WebApi.Extensions;

/// <summary>
/// Extension methods for strings.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Finds all indexes of a substring in a string.
    /// </summary>
    /// <param name="str">The string to search.</param>
    /// <param name="value">The substring to find.</param>
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