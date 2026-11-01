using System;
using System.Globalization;

namespace ShieldBlock.Utils
{
    /// <summary>
    /// Formatação de timestamps em ISO 8601 com timezone explícito.
    /// Padrão aceito em laudos forenses internacionais.
    /// </summary>
    public static class Iso8601
    {
        /// <summary>
        /// Formata DateTime em ISO 8601 com offset de timezone.
        /// Ex: 2026-05-27T14:35:12.345-03:00
        /// </summary>
        public static string Format(DateTime dt)
        {
            DateTimeOffset dto = dt.Kind == DateTimeKind.Utc
                ? new DateTimeOffset(dt, TimeSpan.Zero)
                : new DateTimeOffset(dt);
            return dto.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz", CultureInfo.InvariantCulture);
        }

        public static string FormatNow()
        {
            return Format(DateTime.Now);
        }

        public static string FormatUtcNow()
        {
            return Format(DateTime.UtcNow);
        }

        /// <summary>
        /// Formato amigável para exibição (sem o T): 2026-05-27 14:35:12.345 -03:00
        /// </summary>
        public static string FormatFriendly(DateTime dt)
        {
            DateTimeOffset dto = dt.Kind == DateTimeKind.Utc
                ? new DateTimeOffset(dt, TimeSpan.Zero)
                : new DateTimeOffset(dt);
            return dto.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture);
        }
    }
}
