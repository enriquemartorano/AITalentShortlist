using System.Collections.Concurrent;
using System.Threading.RateLimiting;

namespace TalentShortlist.Api.Security;

public sealed class EvaluationRateLimiter : IDisposable
{
    private readonly FixedWindowRateLimiter globalLimiter;
    private readonly ConcurrentDictionary<string, FixedWindowRateLimiter> ipLimiters = new(StringComparer.OrdinalIgnoreCase);
    private readonly int perIpLimit;
    private readonly int globalLimit;

    public EvaluationRateLimiter(IConfiguration configuration)
    {
        perIpLimit = ReadLimit(configuration, "DemoLimits:EvaluationsPerIpPerHour", 120);
        globalLimit = ReadLimit(configuration, "DemoLimits:GlobalEvaluationsPerHour", 500);
        globalLimiter = CreateLimiter(globalLimit);
    }

    public bool TryAcquire(string ipAddress, out string limitDescription)
    {
        var globalLease = globalLimiter.AttemptAcquire();
        if (!globalLease.IsAcquired)
        {
            globalLease.Dispose();
            limitDescription = "The global hourly evaluation limit has been reached.";
            return false;
        }

        var ipLimiter = ipLimiters.GetOrAdd(ipAddress, _ => CreateLimiter(perIpLimit));
        var ipLease = ipLimiter.AttemptAcquire();
        if (!ipLease.IsAcquired)
        {
            ipLease.Dispose();
            globalLease.Dispose();
            limitDescription = "The hourly evaluation limit for this IP address has been reached.";
            return false;
        }

        ipLease.Dispose();
        globalLease.Dispose();
        limitDescription = string.Empty;
        return true;
    }

    public void Dispose()
    {
        globalLimiter.Dispose();
        foreach (var limiter in ipLimiters.Values)
        {
            limiter.Dispose();
        }
    }

    private static FixedWindowRateLimiter CreateLimiter(int permitLimit) => new(new FixedWindowRateLimiterOptions
    {
        PermitLimit = permitLimit,
        Window = TimeSpan.FromHours(1),
        QueueLimit = 0,
        AutoReplenishment = true
    });

    private static int ReadLimit(IConfiguration configuration, string key, int fallback)
    {
        return int.TryParse(configuration[key], out var value) && value > 0 ? value : fallback;
    }
}
