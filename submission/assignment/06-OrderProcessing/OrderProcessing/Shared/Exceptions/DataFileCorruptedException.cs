namespace OrderProcessing.Shared.Exceptions
{
    public class DataFileCorruptedException : Exception
    {
        public DataFileCorruptedException(string fileName, Exception innerException)
            : base($"Data file '{fileName}' is corrupted or contains invalid JSON.", innerException)
        {
        }
    }
}