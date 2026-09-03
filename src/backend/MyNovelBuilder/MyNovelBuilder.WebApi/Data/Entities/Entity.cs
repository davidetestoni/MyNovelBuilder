using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyNovelBuilder.WebApi.Data.Entities;

/// <summary>
/// An entity.
/// </summary>
public abstract class Entity
{
    /// <summary>
    /// The entity's id.
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Key]
    public Guid Id { get; set; }
}
