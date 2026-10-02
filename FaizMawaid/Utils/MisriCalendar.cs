namespace FaizMawaid.Utils
{
    /// <summary>One Misri (Dawoodi Bohra Hijri) calendar date -- display only, never persisted.</summary>
    public readonly struct MisriDate
    {
        public int Day { get; }
        public int Month { get; }
        public int Year { get; }

        public MisriDate(int day, int month, int year)
        {
            Day = day;
            Month = month;
            Year = year;
        }

        /// <summary>"28 Rabi al-Awwal 1448H".</summary>
        public override string ToString() => $"{Day} {MisriCalendar.MonthNames[Month - 1]} {Year}H";

        /// <summary>"28 Rabi I" -- compact form, no honorific/year, for tight spaces.</summary>
        public string ToShortString() => $"{Day} {MisriCalendar.ShortMonthNames[Month - 1]}";
    }

    /// <summary>
    /// Converts a Gregorian date to its Misri (Dawoodi Bohra Hijri) equivalent -- for DISPLAY
    /// purposes only. The app always stores and queries dates in Gregorian (DateOnly); nothing
    /// in this class is ever written back to the database.
    ///
    /// The Misri calendar is a fixed, tabular calendar -- NOT the generic moon-sighting-based
    /// Hijri calendar used in most countries, which can land a day or two off from this one.
    /// Because it's tabular, it converts deterministically from any Gregorian date with pure
    /// integer arithmetic: no lookup table, no PDF, no network call, and no horizon limit.
    ///
    /// Algorithm: standard Gregorian-to-Julian-Day-Number conversion (Fliegel &amp; Van Flandern),
    /// then an EXACT (non-approximated) walk through whole elapsed Hijri years and whole elapsed
    /// months from the epoch (Julian Day 1,948,085 = 1 Moharram, Hijri year 0), using the 11 leap
    /// ("Kabisa") year-positions per 30-year cycle at (year mod 30) in
    /// {2,5,8,10,13,16,19,21,24,27,29} -- the "Ismaili Tayyebi" tabular scheme, the Dawoodi Bohra
    /// da'wat's own historical name for this variant, confirmed independently (not just
    /// self-consistency) via an outside calendar-systems reference. A Kabisa year's 12th month
    /// (Zilhaj) gets a 30th day; every other month always alternates 30 (odd) / 29 (even).
    ///
    /// This epoch and algorithm were verified against TWO independent real-world anchors: the
    /// Dawat's own published Ashara Mubaraka date (10th Moharram-ul-Haram 1447H = Ashura =
    /// Saturday 5 July 2025), and a reading off a physical printed Misri calendar (2 Oct 2026 =
    /// 21st Rabi al-Aakhar 1448H). An earlier version of this file used epoch 1,948,084 plus a
    /// floating-point day-of-year approximation formula that matched the FIRST anchor only by two
    /// compensating rounding errors, and was off by one day for dates further from the epoch --
    /// caught via the second anchor. See CLAUDE.md, "Misri calendar off-by-one fix", for the full
    /// story. The same algorithm (same constants) is also implemented client-side in
    /// wwwroot/js/site.js (CK.misri) -- that copy is what the UI actually uses; this C# copy
    /// exists so the exact same logic is independently unit-tested here too, not to be called
    /// from any controller today.
    /// </summary>
    public static class MisriCalendar
    {
        public static readonly string[] MonthNames =
        {
            "Moharram ul Haram", "Safar ul Muzaffar", "Rabi al-Awwal", "Rabi al-Aakhar",
            "Jumada al-Ula", "Jumada al-Ukhra", "Rajab al-Asab", "Shaban al-Karim",
            "Ramadan al-Moazzam", "Shawwal al-Mukarram", "Zilqadah al-Haram", "Zilhaj al-Haram"
        };

        public static readonly string[] ShortMonthNames =
        {
            "Moharram", "Safar", "Rabi I", "Rabi II", "Jumada I", "Jumada II",
            "Rajab", "Shaban", "Ramadan", "Shawwal", "Zilqadah", "Zilhaj"
        };

        /// <summary>The 11 leap ("Kabisa") year-positions within each 30-year cycle (0-indexed,
        /// i.e. this is just "year mod 30").</summary>
        private static readonly System.Collections.Generic.HashSet<int> KabisaPositions =
            new System.Collections.Generic.HashSet<int> { 2, 5, 8, 10, 13, 16, 19, 21, 24, 27, 29 };

        private const long EpochJdn = 1948085;

        /// <summary>The Hijri month number (1-12) for Ramadan al-Moazzam.</summary>
        public const int RamadanMonthNumber = 9;

        /// <summary>Converts a Gregorian date to its Misri equivalent.</summary>
        public static MisriDate ToMisriDate(DateOnly gregorianDate)
        {
            var jdn = GregorianToJdn(gregorianDate.Year, gregorianDate.Month, gregorianDate.Day);
            return JdnToMisri(jdn);
        }

        /// <summary>True if this Gregorian date falls within the Hijri month of Ramadan, in any
        /// year (past or future) -- the kitchen doesn't serve food during Ramadan, by the same
        /// kind of fixed, computed-not-stored rule as the Sunday weekly closure.</summary>
        public static bool IsRamadan(DateOnly gregorianDate) => ToMisriDate(gregorianDate).Month == RamadanMonthNumber;

        /// <summary>Gregorian (y, m, d) -&gt; Julian Day Number. Standard proleptic-Gregorian JDN
        /// formula (Fliegel &amp; Van Flandern) -- integer-only, no floating-point rounding risk.</summary>
        private static long GregorianToJdn(int y, int m, int d)
        {
            var a = (14 - m) / 12;
            var yy = y + 4800 - a;
            var mm = m + 12 * a - 3;
            return d + (153L * mm + 2) / 5 + 365L * yy + yy / 4 - yy / 100 + yy / 400 - 32045;
        }

        /// <summary>Julian Day Number -&gt; Misri date. Exact integer arithmetic throughout (no
        /// floating-point day-count approximation) -- walks whole elapsed years, then whole
        /// elapsed months, by their exact fixed lengths, so it can't drift by a day the way an
        /// averaged-year-length formula can.</summary>
        private static MisriDate JdnToMisri(long jdn)
        {
            var d = jdn - EpochJdn; // 0-indexed day count since 1 Moharram, Hijri year 0
            var cyc = (long)Math.Floor(d / 10631.0);
            var remInCycle = d - cyc * 10631; // 0-indexed day within this 30-year cycle (0..10630)

            var j = 0;
            long daysBeforeYear = 0;
            while (true)
            {
                var yearLen = KabisaPositions.Contains(j) ? 355 : 354;
                if (remInCycle < daysBeforeYear + yearLen)
                {
                    break;
                }
                daysBeforeYear += yearLen;
                j += 1;
            }
            var dayOfYear = remInCycle - daysBeforeYear; // 0-indexed day within the Hijri year
            var year = 30 * cyc + j;

            var isLeapYear = KabisaPositions.Contains(j);
            var m = 0;
            var remainingDay = dayOfYear;
            while (true)
            {
                var monthLen = (m % 2 == 0) ? 30 : (m == 11 && isLeapYear ? 30 : 29);
                if (remainingDay < monthLen)
                {
                    break;
                }
                remainingDay -= monthLen;
                m += 1;
            }

            return new MisriDate((int)(remainingDay + 1), m + 1, (int)year);
        }
    }
}
