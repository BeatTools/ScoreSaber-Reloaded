#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#endregion

namespace ScoreSaber.Extensions {
    internal static class TimeAgo {
        public static TimeSpan Days(this int number) {
            return TimeSpan.FromDays(number);
        }

        public static TimeSpan Hours(this int number) {
            return TimeSpan.FromHours(number);
        }

        public static TimeSpan Minutes(this int number) {
            return TimeSpan.FromMinutes(number);
        }

        public static TimeSpan Seconds(this int number) {
            return TimeSpan.FromSeconds(number);
        }

        public static TimeSpan Milliseconds(this int number) {
            return TimeSpan.FromMilliseconds(number);
        }

        public static decimal Round(this decimal @this, int digits) {
            return Math.Round(@this, digits, MidpointRounding.AwayFromZero);
        }

        public static double Round(this double @this, int digits) {
            if (double.IsNaN(@this)) {
                return double.NaN;
            }

            return (double)((decimal)@this).Round(digits);
        }

        private static bool TryReduceDays(ref TimeSpan period, int len, out double result) {
            if (period.TotalDays >= len) {
                result = (int)Math.Floor(period.TotalDays / len);
                period -= TimeSpan.FromDays(len * result);

                return true;
            }

            result = 0;
            return false;
        }

        public static string ToNaturalTime(this TimeSpan @this, int precisionParts, bool longForm) {
            Dictionary<string, string> names = new Dictionary<string, string> {
                { "year", "y" }, { "month", "M" }, { "week", "w" }, { "day", "d" }, { "hour", "h" }, { "minute", "m" },
                { "second", "s" }, { " and ", " " }, { ", ", " " }
            };

            Func<string, string> name = k => longForm ? k : names[k];

            Dictionary<string, double> parts = new Dictionary<string, double>();

            const int YEAR = 365, MONTH = 30, WEEK = 7;

            if (TryReduceDays(ref @this, YEAR, out double years)) {
                parts.Add(name("year"), years);
            }

            if (TryReduceDays(ref @this, MONTH, out double months)) {
                parts.Add(name("month"), months);
            }

            if (TryReduceDays(ref @this, WEEK, out double weeks)) {
                parts.Add(name("week"), weeks);
            }

            if (@this.TotalDays >= 1) {
                parts.Add(name("day"), @this.Days);
                @this -= @this.Days.Days();
            }

            if (@this.TotalHours >= 1 && @this.Hours > 0) {
                parts.Add(name("hour"), @this.Hours);
                @this = @this.Subtract(@this.Hours.Hours());
            }

            if (@this.TotalMinutes >= 1 && @this.Minutes > 0) {
                parts.Add(name("minute"), @this.Minutes);
                @this = @this.Subtract(@this.Minutes.Minutes());
            }

            if (@this.TotalSeconds >= 1 && @this.Seconds > 0) {
                parts.Add(name("second"), @this.Seconds);
                @this = @this.Subtract(@this.Seconds.Seconds());
            } else if (@this.TotalSeconds > 0) {
                parts.Add(name("second"), @this.TotalSeconds.Round(3));
                @this = TimeSpan.Zero;
            }

            List<KeyValuePair<string, double>> outputParts = parts.Take(precisionParts).ToList();
            StringBuilder r = new StringBuilder();

            foreach (KeyValuePair<string, double> part in outputParts) {
                r.Append(part.Value);

                if (longForm) {
                    r.Append(" ");
                }

                r.Append(part.Key);

                if (part.Value > 1 && longForm) {
                    r.Append("s");
                }

                if (outputParts.IndexOf(part) == outputParts.Count - 2) {
                    r.Append(name(" and "));
                } else if (outputParts.IndexOf(part) < outputParts.Count - 2) {
                    r.Append(name(", "));
                }
            }

            return r.ToString();
        }
    }
}