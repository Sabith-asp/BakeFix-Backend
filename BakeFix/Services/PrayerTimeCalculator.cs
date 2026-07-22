using BakeFix.Models;

namespace BakeFix.Services
{
    public class CalculatedPrayerTimes
    {
        public TimeSpan Fajr { get; init; }
        public TimeSpan Sunrise { get; init; }
        public TimeSpan Dhuhr { get; init; }
        public TimeSpan Asr { get; init; }
        public TimeSpan Maghrib { get; init; }
        public TimeSpan Isha { get; init; }

        public TimeSpan GetTime(string prayerName) => prayerName switch
        {
            PrayerNames.Fajr    => Fajr,
            PrayerNames.Dhuhr   => Dhuhr,
            PrayerNames.Asr     => Asr,
            PrayerNames.Maghrib => Maghrib,
            PrayerNames.Isha    => Isha,
            _ => throw new ArgumentException($"Unknown prayer: {prayerName}")
        };

        // End of each prayer window = start of next prayer
        public TimeSpan GetEndTime(string prayerName) => prayerName switch
        {
            PrayerNames.Fajr    => Sunrise,
            PrayerNames.Dhuhr   => Asr,
            PrayerNames.Asr     => Maghrib,
            PrayerNames.Maghrib => Isha,
            PrayerNames.Isha    => TimeSpan.FromHours(24),
            _ => throw new ArgumentException($"Unknown prayer: {prayerName}")
        };

        public string? GetCurrentPrayer(TimeSpan localTime)
        {
            if (localTime >= Isha)    return PrayerNames.Isha;
            if (localTime >= Maghrib) return PrayerNames.Maghrib;
            if (localTime >= Asr)     return PrayerNames.Asr;
            if (localTime >= Dhuhr)   return PrayerNames.Dhuhr;
            if (localTime >= Fajr)    return PrayerNames.Fajr;
            return null;
        }

        public (string Name, TimeSpan Time)? GetNextPrayer(TimeSpan localTime)
        {
            if (localTime < Fajr)    return (PrayerNames.Fajr, Fajr);
            if (localTime < Dhuhr)   return (PrayerNames.Dhuhr, Dhuhr);
            if (localTime < Asr)     return (PrayerNames.Asr, Asr);
            if (localTime < Maghrib) return (PrayerNames.Maghrib, Maghrib);
            if (localTime < Isha)    return (PrayerNames.Isha, Isha);
            return null; // after Isha — next is tomorrow's Fajr
        }
    }

    public static class PrayerTimeCalculator
    {
        private static double DegToRad(double d) => d * Math.PI / 180.0;
        private static double RadToDeg(double r) => r * 180.0 / Math.PI;

        private static double JulianDate(DateTime date)
        {
            int Y = date.Year, M = date.Month, D = date.Day;
            if (M <= 2) { Y--; M += 12; }
            int A = Y / 100;
            int B = 2 - A + A / 4;
            return Math.Floor(365.25 * (Y + 4716))
                 + Math.Floor(30.6001 * (M + 1))
                 + D + B - 1524.5;
        }

        private static TimeSpan HoursToTimeSpan(double h)
        {
            h %= 24;
            if (h < 0) h += 24;
            return TimeSpan.FromSeconds(Math.Round(h * 3600));
        }

        // T(angle): hours offset from solar noon for a given sun depression angle
        private static double TimeForAngle(double angle, double lat, double dec)
        {
            double cosA = (-Math.Sin(DegToRad(angle))
                          - Math.Sin(DegToRad(lat)) * Math.Sin(DegToRad(dec)))
                         / (Math.Cos(DegToRad(lat)) * Math.Cos(DegToRad(dec)));

            if (cosA < -1 || cosA > 1) return double.NaN;
            return RadToDeg(Math.Acos(cosA)) / 15.0;
        }

        private static (double Fajr, double Isha) GetAngles(string method, double customFajr, double customIsha)
            => method switch
            {
                "ISNA"     => (15.0, 15.0),
                "Egyptian" => (19.5, 17.5),
                "MWL"      => (18.0, 17.0),
                "Karachi"  => (18.0, 18.0),   // University of Islamic Sciences, Karachi
                "UmmAlQura"=> (18.5, -1),      // Isha = Maghrib + 90 min
                "Custom"   => (customFajr, customIsha),
                _          => (18.0, 17.0)
            };

        public static CalculatedPrayerTimes Calculate(EffectivePrayerSettings s, DateTime localDate)
        {
            TimeZoneInfo tzi;
            try { tzi = TimeZoneInfo.FindSystemTimeZoneById(s.Timezone); }
            catch { tzi = TimeZoneInfo.Utc; }

            double tzOffset = tzi.GetUtcOffset(localDate).TotalHours;

            double D = JulianDate(localDate) - 2451545.0;

            double g = (357.529 + 0.98560028 * D) % 360;
            double q = (280.459 + 0.98564736 * D) % 360;
            double L = (q + 1.915 * Math.Sin(DegToRad(g))
                          + 0.020 * Math.Sin(DegToRad(2 * g))) % 360;
            double e = 23.439 - 0.00000036 * D;

            double RA = RadToDeg(Math.Atan2(
                Math.Cos(DegToRad(e)) * Math.Sin(DegToRad(L)),
                Math.Cos(DegToRad(L)))) / 15.0;

            double dec = RadToDeg(Math.Asin(Math.Sin(DegToRad(e)) * Math.Sin(DegToRad(L))));
            double eqT = q / 15.0 - RA;
            double Tnoon = 12.0 + tzOffset - s.Longitude / 15.0 - eqT;

            double asrFactor = s.AsrMethod == "Hanafi" ? 2.0 : 1.0;
            double asrAngle  = RadToDeg(Math.Atan(
                1.0 / (asrFactor + Math.Tan(DegToRad(Math.Abs(s.Latitude - dec))))));

            var (fajrAngle, ishaAngle) = GetAngles(s.CalculationMethod, s.FajrAngle, s.IshaAngle);

            double fajr    = Tnoon - TimeForAngle(fajrAngle, s.Latitude, dec);
            double sunrise = Tnoon - TimeForAngle(0.833, s.Latitude, dec);
            double dhuhr   = Tnoon;
            double asr     = Tnoon + TimeForAngle(-asrAngle, s.Latitude, dec);
            double maghrib = Tnoon + TimeForAngle(0.833, s.Latitude, dec);
            double isha    = s.CalculationMethod == "UmmAlQura"
                             ? maghrib + 1.5
                             : Tnoon + TimeForAngle(ishaAngle, s.Latitude, dec);

            return new CalculatedPrayerTimes
            {
                Fajr    = HoursToTimeSpan(fajr),
                Sunrise = HoursToTimeSpan(sunrise),
                Dhuhr   = HoursToTimeSpan(dhuhr),
                Asr     = HoursToTimeSpan(asr),
                Maghrib = HoursToTimeSpan(maghrib),
                Isha    = HoursToTimeSpan(isha)
            };
        }
    }
}
