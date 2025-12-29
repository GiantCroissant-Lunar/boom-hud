using BoomHud.Abstractions.IR;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.IR;

public class ColorTests
{
    [Theory]
    [InlineData("black", 0, 0, 0)]
    [InlineData("white", 255, 255, 255)]
    [InlineData("red", 255, 0, 0)]
    [InlineData("green", 0, 255, 0)]
    [InlineData("blue", 0, 0, 255)]
    public void Parse_NamedColor_ReturnsCorrectRgb(string name, byte r, byte g, byte b)
    {
        var color = Color.Parse(name);

        color.R.Should().Be(r);
        color.G.Should().Be(g);
        color.B.Should().Be(b);
        color.A.Should().Be(255);
    }

    [Theory]
    [InlineData("#f00", 255, 0, 0)]
    [InlineData("#0f0", 0, 255, 0)]
    [InlineData("#00f", 0, 0, 255)]
    public void Parse_ShortHex_ReturnsCorrectRgb(string hex, byte r, byte g, byte b)
    {
        var color = Color.Parse(hex);

        color.R.Should().Be(r);
        color.G.Should().Be(g);
        color.B.Should().Be(b);
    }

    [Theory]
    [InlineData("#ff0000", 255, 0, 0)]
    [InlineData("#00ff00", 0, 255, 0)]
    [InlineData("#0000ff", 0, 0, 255)]
    [InlineData("#222222", 34, 34, 34)]
    public void Parse_FullHex_ReturnsCorrectRgb(string hex, byte r, byte g, byte b)
    {
        var color = Color.Parse(hex);

        color.R.Should().Be(r);
        color.G.Should().Be(g);
        color.B.Should().Be(b);
    }

    [Fact]
    public void ToHex_ReturnsCorrectFormat()
    {
        var color = new Color(255, 128, 64);

        color.ToHex().Should().Be("#FF8040");
    }

    [Fact]
    public void ToHex_WithAlpha_IncludesAlpha()
    {
        var color = new Color(255, 128, 64, 128);

        color.ToHex().Should().Be("#FF804080");
    }
}
