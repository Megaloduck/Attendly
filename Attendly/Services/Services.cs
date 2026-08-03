using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Attendly.Data;

namespace Attendly.Services;

/// <summary>
/// Registers the shared core services (repository, path resolution, clock).
/// Each platform head calls AddAttendlyCore() once at startup.
///
/// If a platform head needs its own IAppPathProvider (Android/iOS in Phase 5),
/// register it BEFORE calling AddAttendlyCore() - TryAddSingleton below only
/// fills in the default if nothing is registered yet.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAttendlyCore(this IServiceCollection services)
    {
        services.TryAddSingleton<IAppPathProvider, DefaultAppPathProvider>();
        services.TryAddSingleton<IClock, SystemClock>();
        services.TryAddSingleton<ILocalSyncService, LocalSyncService>();

        // One shared SQLite connection for the app's lifetime.
        services.AddSingleton<AttendanceRepository>();

        return services;
    }
}

/// <summary>Thin wrapper over DateTime.UtcNow so tests can control "now".</summary>
public interface IClock
{
    long UtcNowTicks { get; }
}

public class SystemClock : IClock
{
    public long UtcNowTicks => System.DateTime.UtcNow.Ticks;
}
