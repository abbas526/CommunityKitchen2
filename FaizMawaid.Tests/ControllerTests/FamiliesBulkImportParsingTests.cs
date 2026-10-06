using FaizMawaid.Controllers;
using Xunit;

namespace FaizMawaid.Tests.ControllerTests
{
    public class FamiliesBulkImportParsingTests
    {
        [Theory]
        [InlineData(null, true, true, true)]
        [InlineData("", true, true, true)]
        [InlineData("   ", true, true, true)]
        [InlineData("Yes", true, true, true)]
        [InlineData("yes", false, true, true)]
        [InlineData("Y", false, true, true)]
        [InlineData("No", true, true, false)]
        [InlineData("no", true, true, false)]
        [InlineData("N", true, true, false)]
        public void TryParseYesNo_AcceptsYesNoAndBlank(string? raw, bool defaultValue, bool expectedOk, bool expectedValue)
        {
            var ok = FamiliesController.TryParseYesNo(raw, defaultValue, out var value);

            Assert.Equal(expectedOk, ok);
            Assert.Equal(expectedValue, value);
        }

        [Theory]
        [InlineData("maybe")]
        [InlineData("Nope")]
        [InlineData("2")]
        public void TryParseYesNo_RejectsAnythingElse(string raw)
        {
            Assert.False(FamiliesController.TryParseYesNo(raw, true, out _));
        }
    }
}
