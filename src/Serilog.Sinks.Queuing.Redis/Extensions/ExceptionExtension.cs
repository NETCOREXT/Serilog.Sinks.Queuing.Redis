namespace Serilog.Sinks.Queuing.Redis.Extensions;

public static class ExceptionExtension
{
    public static Exception GetInnerException(this Exception e)
    {
        var ex = e;

        while (ex.InnerException != null)
        {
            ex = ex.InnerException;
        }

        return ex;
    }
}