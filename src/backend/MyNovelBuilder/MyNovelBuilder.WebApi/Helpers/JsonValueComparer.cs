using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace MyNovelBuilder.WebApi.Helpers;

/// <summary>
/// A value comparer that compares values by serializing them to JSON.
/// </summary>
public class JsonValueComparer<T> : ValueComparer<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonValueComparer{T}"/> class.
    /// </summary>
    public JsonValueComparer() : base(
        (a, b) => ReferenceEquals(a, b) // short-circuit for reference equality
                  || !(object.Equals(a, default(T)) || object.Equals(b, default(T))) // handle nulls
                  || JsonSerializer.Serialize<T>(a!) == JsonSerializer.Serialize<T>(b!), // deep comparison
        v => object.Equals(v, default(T)) ? 0 : JsonSerializer.Serialize(v).GetHashCode(),
        v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v))!
    ) { }
}
