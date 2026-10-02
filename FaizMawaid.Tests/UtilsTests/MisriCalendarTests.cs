using FaizMawaid.Utils;
using Xunit;

namespace FaizMawaid.Tests.UtilsTests
{
    /// <summary>
    /// Pure-math tests for the Misri (Dawoodi Bohra Hijri) calendar converter -- no DB, no mocks.
    /// The anchor tests check this against TWO independent, real-world dates (not just internal
    /// self-consistency): the Dawat's own published Ashara Mubaraka date for 1447H, and a reading
    /// off a physical printed Misri calendar for 1448H. Both are needed -- an earlier version of
    /// this algorithm matched the first anchor alone by two compensating rounding errors and was
    /// a day off everywhere else. See MisriCalendar.cs's class-level doc comment for the full story.
    /// </summary>
    public class MisriCalendarTests
    {
        [Fact]
        public void ToMisriDate_MatchesThePubliclyPublishedAshuraDateFor1447H()
        {
            // 10th Moharram-ul-Haram 1447H (Ashura) was published by the Dawat as Saturday 5 July 2025.
            var result = MisriCalendar.ToMisriDate(new DateOnly(2025, 7, 5));

            Assert.Equal(10, result.Day);
            Assert.Equal(1, result.Month);
            Assert.Equal(1447, result.Year);
        }

        [Theory]
        [InlineData(2025, 7, 4, 9, 1, 1447)]
        [InlineData(2025, 7, 6, 11, 1, 1447)]
        public void ToMisriDate_AdjacentDaysAreOneDayApart(int gy, int gm, int gd, int expectedDay, int expectedMonth, int expectedYear)
        {
            var result = MisriCalendar.ToMisriDate(new DateOnly(gy, gm, gd));

            Assert.Equal(expectedDay, result.Day);
            Assert.Equal(expectedMonth, result.Month);
            Assert.Equal(expectedYear, result.Year);
        }

        [Fact]
        public void ToMisriDate_MatchesAPhysicalPrintedMisriCalendarFor1448H()
        {
            // Read directly off a physical printed Misri calendar: 2 Oct 2026 = 21st Rabi al-Aakhar 1448H.
            var result = MisriCalendar.ToMisriDate(new DateOnly(2026, 10, 2));

            Assert.Equal(21, result.Day);
            Assert.Equal(4, result.Month);
            Assert.Equal(1448, result.Year);
        }

        [Theory]
        [InlineData(2026, 10, 1, 20, 4, 1448)]
        [InlineData(2026, 10, 3, 22, 4, 1448)]
        public void ToMisriDate_AdjacentDaysAreOneDayApartNearTheSecondAnchor(int gy, int gm, int gd, int expectedDay, int expectedMonth, int expectedYear)
        {
            var result = MisriCalendar.ToMisriDate(new DateOnly(gy, gm, gd));

            Assert.Equal(expectedDay, result.Day);
            Assert.Equal(expectedMonth, result.Month);
            Assert.Equal(expectedYear, result.Year);
        }

        [Fact]
        public void ToString_FormatsWithHonorificMonthNameAndHSuffix()
        {
            var result = MisriCalendar.ToMisriDate(new DateOnly(2025, 7, 5));

            Assert.Equal("10 Moharram ul Haram 1447H", result.ToString());
        }

        [Fact]
        public void ToShortString_FormatsWithoutHonorificOrYear()
        {
            var result = MisriCalendar.ToMisriDate(new DateOnly(2025, 7, 5));

            Assert.Equal("10 Moharram", result.ToShortString());
        }

        /// <summary>
        /// Walks 20 consecutive Misri years day-by-day (via their Gregorian equivalents) and checks
        /// every month's actual length against the documented Bohra tabular rule: odd months always
        /// 30 days, even months (other than the 12th) always 29 days, and the 12th month is 30 days
        /// in a "Kabisa" (leap) year -- remainder of (year mod 30) in {2,5,8,10,13,16,19,21,24,27,29}
        /// -- and 29 days otherwise. This is the real correctness test for the whole algorithm, not
        /// just a couple of spot-checked dates.
        /// </summary>
        [Fact]
        public void ToMisriDate_MonthLengthsFollowTheBohraTabularKabisaRule()
        {
            var kabisaRemainders = new HashSet<int> { 2, 5, 8, 10, 13, 16, 19, 21, 24, 27, 29 };
            var monthLengthsByYearMonth = new Dictionary<(int Year, int Month), int>();

            var date = new DateOnly(2020, 1, 1);
            var end = new DateOnly(2040, 12, 31);
            while (date <= end)
            {
                var m = MisriCalendar.ToMisriDate(date);
                var key = (m.Year, m.Month);
                monthLengthsByYearMonth[key] = monthLengthsByYearMonth.TryGetValue(key, out var existing)
                    ? Math.Max(existing, m.Day)
                    : m.Day;
                date = date.AddDays(1);
            }

            // Drop the first and last partially-observed year so every month counted below is complete.
            var completeMonths = monthLengthsByYearMonth
                .Where(kv => kv.Key.Year > 1441 && kv.Key.Year < 1463)
                .ToList();
            Assert.True(completeMonths.Count > 12 * 15, "expected many complete months to check");

            foreach (var (key, length) in completeMonths)
            {
                var (year, month) = key;
                if (month == 12)
                {
                    var isKabisa = kabisaRemainders.Contains(year % 30);
                    Assert.Equal(isKabisa ? 30 : 29, length);
                }
                else if (month % 2 == 1)
                {
                    Assert.Equal(30, length);
                }
                else
                {
                    Assert.Equal(29, length);
                }
            }
        }

        /// <summary>IsRamadan is the predicate the kitchen's "non-serving day" rule relies on --
        /// checked against both Ramadan boundaries in two different Hijri years, not just one.</summary>
        [Theory]
        [InlineData(2027, 2, 5, false)]  // last day of Shaban al-Karim 1448H
        [InlineData(2027, 2, 6, true)]   // 1st Ramadan al-Moazzam 1448H
        [InlineData(2027, 3, 7, true)]   // 30th (last day of) Ramadan al-Moazzam 1448H
        [InlineData(2027, 3, 8, false)]  // 1st Shawwal al-Mukarram 1448H
        [InlineData(2028, 1, 27, true)]  // 1st Ramadan al-Moazzam 1449H
        [InlineData(2028, 2, 25, true)]  // last day of Ramadan al-Moazzam 1449H
        [InlineData(2028, 2, 26, false)] // 1st Shawwal al-Mukarram 1449H
        public void IsRamadan_MatchesAtBothRamadanBoundariesInTwoDifferentYears(int gy, int gm, int gd, bool expected)
        {
            Assert.Equal(expected, MisriCalendar.IsRamadan(new DateOnly(gy, gm, gd)));
        }

        /// <summary>Day/month are always within valid range for any date, with no gaps or
        /// out-of-bounds values, across a wide span including several Kabisa-year boundaries.</summary>
        [Fact]
        public void ToMisriDate_StaysInValidRangeAcrossManyYears()
        {
            var date = new DateOnly(2015, 1, 1);
            var end = new DateOnly(2045, 12, 31);
            while (date <= end)
            {
                var m = MisriCalendar.ToMisriDate(date);
                Assert.InRange(m.Day, 1, 30);
                Assert.InRange(m.Month, 1, 12);
                date = date.AddDays(1);
            }
        }
    }
}
