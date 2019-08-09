namespace ngdb.Models
{
    public class Document<T>
    {
        public string Key { get; set; }
        public long Cas { get; set; }
        public T Value { get; set; }
    }
}
