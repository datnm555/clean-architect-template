namespace SharedKernel;

public abstract class AuditedEntity : Entity
{
    public DateTime CreatedAt { get; internal set; }
    public DateTime ModifiedAt { get; internal set; }
}
