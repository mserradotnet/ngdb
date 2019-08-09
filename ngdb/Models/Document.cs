namespace ngdb.Models
{
    public class Document<T>
    {
        public string Collection { get; set; }
        public string Key { get; set; }
        public long Cas { get; set; }
        public T Value { get; set; }

        public override bool Equals(object obj) => obj != null && obj is Document<T> && ((Document<T>)obj).Collection == Collection && ((Document<T>)obj).Key == Key && ((Document<T>)obj).Cas == Cas;
        public override int GetHashCode() => $"{Collection ?? string.Empty}{Key ?? string.Empty}{Cas}".GetHashCode();
    }
}
