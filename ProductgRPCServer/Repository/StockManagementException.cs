namespace ProductgRPCServer.Repository
{
    public class StockManagementException : Exception
    {
        public StockManagementException(string message) : base(message)
        {
        }

        public StockManagementException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
