using System.Text.RegularExpressions;
using ContosoDashboard.Services.Documents;
using FluentAssertions;
using Xunit;

namespace ContosoDashboard.Tests.Unit.Services.Documents;

public class FilePathBuilderTests
{
    private readonly FilePathBuilder _sut = new();

    [Fact]
    public void BuildPath_ShouldReturnExpectedPattern_ForPersonalDocument()
    {
        var path = _sut.BuildPath("ni.kang@contoso.com", null, "pdf");

        path.Should().StartWith("nikangcontosocom/personal/");
        Regex.IsMatch(path, @"^[\w\-]+/(?:[\w\-]+|personal)/[a-f0-9]{32}\.pdf$", RegexOptions.IgnoreCase)
            .Should().BeTrue();
    }

    [Fact]
    public void IsValidPath_ShouldReturnFalse_ForTraversalAttempt()
    {
        var result = _sut.IsValidPath("user/personal/../../secrets.txt");

        result.Should().BeFalse();
    }
}
