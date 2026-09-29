using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kruty1918.Diagnostics
{
    public enum HealthStatus
    {
        /// <summary>Модуль повністю готовий до роботи.</summary>
        Ready = 0,
        /// <summary>Модуль працює, але з деградованою якістю (напр. Relay недоступний).</summary>
        Degraded = 1,
        /// <summary>Модуль повністю недоступний, функціонал заблоковано.</summary>
        Unavailable = 2,
    }

    public sealed class HealthCheckResult
    {
        public string Module { get; }
        public HealthStatus Status { get; }
        public string Detail { get; }
        public DateTime CheckedAt { get; }

        public HealthCheckResult(string module, HealthStatus status, string detail = "")
        {
            Module = module ?? string.Empty;
            Status = status;
            Detail = detail ?? string.Empty;
            CheckedAt = DateTime.UtcNow;
        }

        public override string ToString() =>
            $"[Health] {Module}: {Status}" + (string.IsNullOrWhiteSpace(Detail) ? string.Empty : $" — {Detail}");
    }

    /// <summary>
    /// Агрегатор стану готовності всіх зареєстрованих модулів.
    /// Bind у SharedInstaller; репортери реєструються через <see cref="IHealthReporter"/>.
    /// </summary>
    public interface IHealthCheckService
    {
        /// <summary>Повертає поточний стан усіх модулів.</summary>
        IReadOnlyList<HealthCheckResult> GetAll();

        /// <summary>Повертає стан конкретного модуля або <c>null</c>, якщо не зареєстровано.</summary>
        HealthCheckResult Get(string module);

        /// <summary>
        /// <c>true</c> якщо всі зареєстровані модулі мають статус <see cref="HealthStatus.Ready"/>.
        /// </summary>
        bool IsFullyHealthy { get; }
    }

    /// <summary>
    /// Реалізовується кожним модулем, що хоче звітувати про свій стан.
    /// Bind у відповідному інсталері як <c>Bind&lt;IHealthReporter&gt;().To&lt;...&gt;()</c>.
    /// </summary>
    public interface IHealthReporter
    {
        HealthCheckResult Report();
    }

    public sealed class HealthCheckService : IHealthCheckService
    {
        private readonly List<IHealthReporter> _reporters;
        private readonly List<HealthCheckResult> _cache = new List<HealthCheckResult>();

        public HealthCheckService(List<IHealthReporter> reporters)
        {
            _reporters = reporters ?? new List<IHealthReporter>();
        }

        public void Initialize()
        {
            Refresh();
        }

        public IReadOnlyList<HealthCheckResult> GetAll()
        {
            Refresh();
            return _cache;
        }

        public HealthCheckResult Get(string module)
        {
            if (string.IsNullOrWhiteSpace(module))
                return null;

            Refresh();
            foreach (var r in _cache)
                if (string.Equals(r.Module, module, StringComparison.OrdinalIgnoreCase))
                    return r;

            return null;
        }

        public bool IsFullyHealthy
        {
            get
            {
                foreach (var r in _cache)
                    if (r.Status != HealthStatus.Ready)
                        return false;
                return true;
            }
        }

        private void Refresh()
        {
            _cache.Clear();
            foreach (var reporter in _reporters)
            {
                try
                {
                    _cache.Add(reporter.Report());
                }
                catch (Exception ex)
                {
                    _cache.Add(new HealthCheckResult(reporter.GetType().Name, HealthStatus.Unavailable, ex.Message));
                }
            }
        }
    }

    // ---------------------------------------------------------------------------
    // Built-in health reporters
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Перевіряє наявність підключення до Інтернету через <see cref="Application.internetReachability"/>.
    /// </summary>
    public sealed class InternetConnectivityHealthReporter : IHealthReporter
    {
        public HealthCheckResult Report()
        {
            var reachability = Application.internetReachability;
            var status = reachability == NetworkReachability.NotReachable
                ? HealthStatus.Unavailable
                : HealthStatus.Ready;
            return new HealthCheckResult("connectivity", status, reachability.ToString());
        }
    }

    /// <summary>
    /// Мінімальний репортер, який завжди повідомляє Ready. Корисний для заглушок під час тестів.
    /// </summary>
    public sealed class AlwaysReadyHealthReporter : IHealthReporter
    {
        private readonly string _module;

        public AlwaysReadyHealthReporter(string module) => _module = module;

        public HealthCheckResult Report() =>
            new HealthCheckResult(_module, HealthStatus.Ready, "stub reporter");
    }
}
