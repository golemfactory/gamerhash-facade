namespace Golem
{
    public static class ExceptionsExtension
    {
        public static bool IsCancelled(this Exception e)
        {
            return e is OperationCanceledException
                || e is AggregateException && e.InnerException is OperationCanceledException;
        }
    }
}
