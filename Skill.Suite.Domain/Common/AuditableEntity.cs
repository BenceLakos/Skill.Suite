namespace Skill.Suite.Domain.Common;

public abstract class AuditableEntity<TId> : Entity<TId>, IAuditable where TId : notnull
{
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
