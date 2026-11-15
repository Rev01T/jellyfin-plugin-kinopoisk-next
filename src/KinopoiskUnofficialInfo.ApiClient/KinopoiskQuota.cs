using System;

namespace KinopoiskUnofficialInfo.ApiClient
{
    /// <summary>
    /// Schedule of the daily Kinopoisk Unofficial request quota.
    /// </summary>
    /// <remarks>
    /// The daily counter is reset at 03:00 Moscow time. Moscow has been on UTC+3 without
    /// daylight saving time since 2014, so a fixed offset is used instead of a time zone id,
    /// which differs between Windows and Linux.
    /// </remarks>
    public static class KinopoiskQuota
    {
        private static readonly TimeSpan MoscowOffset = TimeSpan.FromHours(3);
        private static readonly TimeSpan ResetTimeOfDay = TimeSpan.FromHours(3);

        /// <summary>How long requests are paused after the quota was found exhausted.</summary>
        public static readonly TimeSpan Cooldown = TimeSpan.FromHours(1);

        /// <summary>
        /// Right after the reset the API may still answer "quota exceeded" for a short while,
        /// so the pause is short during this period.
        /// </summary>
        public static readonly TimeSpan ResetGracePeriod = TimeSpan.FromMinutes(15);

        public static readonly TimeSpan GraceCooldown = TimeSpan.FromMinutes(1);

        public static DateTimeOffset PreviousReset(DateTimeOffset now)
        {
            var moscow = now.ToOffset(MoscowOffset);
            var reset = new DateTimeOffset(moscow.Year, moscow.Month, moscow.Day, 0, 0, 0, MoscowOffset) + ResetTimeOfDay;
            return reset > now ? reset.AddDays(-1) : reset;
        }

        public static DateTimeOffset NextReset(DateTimeOffset now) => PreviousReset(now).AddDays(1);

        /// <summary>
        /// The moment until which requests are paused after the API answered "quota exceeded" at
        /// <paramref name="now"/>. The pause never extends past the next daily reset.
        /// </summary>
        public static DateTimeOffset BlockedUntil(DateTimeOffset now)
        {
            var cooldown = now - PreviousReset(now) < ResetGracePeriod ? GraceCooldown : Cooldown;
            var until = now + cooldown;
            var nextReset = NextReset(now);
            return until < nextReset ? until : nextReset;
        }
    }
}
