namespace ngdb.Models
{
    public class NgDbConfig
    {
        public NgDbConfig()
        {
            //InMemoryOnly = true;
            //MaxParallelism = 4;
            //PersistenceFolder = "C:\\ngdb_data";
            //SynchronousPersistence = false;
            //SetTimeoutInMilliseconds = 50;
        }

        public bool InMemoryOnly { get; set; }
        public int MaxParallelism { get; set; }
        public string PersistenceFolder { get; set; }
        public bool SynchronousPersistence { get; set; }
        public int SetTimeoutInMilliseconds { get; set; }
    }
}
