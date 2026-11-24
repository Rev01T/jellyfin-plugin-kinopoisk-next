using System;

namespace KinopoiskUnofficialInfo.ApiClient
{
    /// <summary>
    /// Pause of the requests after the daily limit of the token is exhausted.
    /// </summary>
    /// <remarks>
    /// The API reports the reset time itself (<see cref="TokenInfoDto.ResetAt"/>), so no
    /// fixed schedule is assumed.
    /// </remarks>
    public static class KinopoiskQuota
    {
        /// <summary>Pause when the API did not say when the quota is reset.</summary>
        public static readonly TimeSpan FallbackCooldown = TimeSpan.FromHours(1);

        /// <summary>
        /// Pause when the reset time has already passed but the API still reports no requests left:
        /// the counter may lag behind the reset for a short while.
        /// </summary>
        public static readonly TimeSpan GraceCooldown = TimeSpan.FromMinutes(1);

        /// <summary>The pause lasts a little longer than the announced reset time.</summary>
        public static readonly TimeSpan ResetMargin = TimeSpan.FromSeconds(5);

        /// <summary>A pause is never longer than this, whatever the API says.</summary>
        public static readonly TimeSpan MaxCooldown = TimeSpan.FromHours(25);

        /// <summary>
        /// The moment until which requests are paused after the quota was found exhausted at
        /// <paramref name="now"/>.
        /// </summary>
        /// <param name="now">Current time.</param>
        /// <param name="resetAt">Reset time from the token info, if known.</param>
        /// <param name="ttlSeconds">Seconds until the reset from the token info, if known.</param>
        public static DateTimeOffset BlockedUntil(DateTimeOffset now, DateTimeOffset? resetAt, int? ttlSeconds = null)
        {
            DateTimeOffset until;
            if (resetAt.HasValue && resetAt.Value > now)
                until = resetAt.Value + ResetMargin;
            else if (resetAt.HasValue)
                until = now + GraceCooldown;
            else if (ttlSeconds.HasValue && ttlSeconds.Value > 0)
                until = now + TimeSpan.FromSeconds(ttlSeconds.Value) + ResetMargin;
            else
                until = now + FallbackCooldown;

            var limit = now + MaxCooldown;
            return until < limit ? until : limit;
        }

        /// <summary>Converts the reset time of the token info (UTC) to an offset time.</summary>
        public static DateTimeOffset? ToResetTime(DateTime? resetAt)
        {
            if (!resetAt.HasValue)
                return null;

            var value = resetAt.Value;
            // The API sends UTC; a value without a kind is treated as UTC too.
            if (value.Kind != DateTimeKind.Utc)
                value = value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
            return new DateTimeOffset(value);
        }
    }
}
