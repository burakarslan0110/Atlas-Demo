using System.Diagnostics;

namespace Atlas.Tracing;




public static class ActivitySourceProvider
{
    private static readonly Dictionary<string, ActivitySource> _sources = new();
    private static readonly object _lock = new();






    public static ActivitySource GetSource(string serviceName)
    {
        if (_sources.TryGetValue(serviceName, out var source))
        {
            return source;
        }

        lock (_lock)
        {
            if (_sources.TryGetValue(serviceName, out source))
            {
                return source;
            }

            source = new ActivitySource(serviceName);
            _sources[serviceName] = source;
            return source;
        }
    }





    internal static void RegisterSource(string serviceName)
    {
        GetSource(serviceName);
    }








    public static Activity? StartActivity(
        string serviceName,
        string activityName,
        ActivityKind kind = ActivityKind.Internal)
    {
        var source = GetSource(serviceName);
        return source.StartActivity(activityName, kind);
    }









    public static Activity? StartActivity(
        string serviceName,
        string activityName,
        ActivityKind kind,
        IEnumerable<KeyValuePair<string, object?>> tags)
    {
        var source = GetSource(serviceName);
        return source.StartActivity(activityName, kind, default(ActivityContext), tags);
    }




    public static void AddEvent(this Activity? activity, string eventName)
    {
        activity?.AddEvent(new ActivityEvent(eventName));
    }




    public static void AddEvent(this Activity? activity, string eventName, ActivityTagsCollection tags)
    {
        activity?.AddEvent(new ActivityEvent(eventName, DateTimeOffset.UtcNow, tags));
    }




    public static void RecordException(this Activity? activity, Exception exception)
    {
        if (activity == null) return;

        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
        {
            ["exception.type"] = exception.GetType().FullName,
            ["exception.message"] = exception.Message,
            ["exception.stacktrace"] = exception.StackTrace
        }));
    }




    public static void SetOkStatus(this Activity? activity)
    {
        activity?.SetStatus(ActivityStatusCode.Ok);
    }




    public static void SetErrorStatus(this Activity? activity, string description)
    {
        activity?.SetStatus(ActivityStatusCode.Error, description);
    }
}
