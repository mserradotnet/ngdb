namespace ngdb.Models
{
    public class Collection
    {
        public string Name { get; set; }
        public int ItemCount { get; set; }
        public bool PersistenceEnabled { get; set; }

        //public CollectionType Type { get; set; }

        public override bool Equals(object obj) => obj != null && obj is Collection && ((Collection)obj).Name == Name;
        public override int GetHashCode() => (Name ?? string.Empty).GetHashCode();
    }

    /// <summary>
    /// Defines the type of the collection. Defaults to Raw.
    /// <see cref="Raw"/> stores whatever value as-is, in memory, as POCO. This is for use cases where <see cref="ngdb"/> is included directly as a NuGet to serve as a full in-memory database. 
    /// <see cref="Json"/> stores whatever value in a Json serialized way. This is for use cases where <see cref="ngdb"/> is used as a database server and is beeing accessed by remote clients.
    /// </summary>
    //public enum CollectionType
    //{
    //    Raw,
    //    Json
    //}
}
