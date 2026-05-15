namespace Skill.Suite.Domain.Common;

/// <summary>
/// Marker interface for entities that should have audit timestamps applied
/// automatically on SaveChanges by the persistence layer.
/// </summary>
public interface IAuditable
{
    DateTime CreatedAt { get; set; }
    string? CreatedBy { get; set; }
    DateTime? UpdatedAt { get; set; }
    string? UpdatedBy { get; set; }
}
