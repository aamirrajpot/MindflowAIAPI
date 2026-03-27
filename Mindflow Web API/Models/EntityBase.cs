namespace Mindflow_Web_API.Models
{
    public abstract class EntityBase
    {
        public Guid Id { get; private init; } = Guid.NewGuid();
        // SQLite-safe audit fields for server-side ordering/filtering.
        // Always stored in UTC.
        public DateTime Created { get; private set; } = DateTime.UtcNow;
        public DateTime LastModified { get; private set; } = DateTime.UtcNow;
        public void UpdateLastModified()
        {
            LastModified = DateTime.UtcNow;
        }
    }
}
