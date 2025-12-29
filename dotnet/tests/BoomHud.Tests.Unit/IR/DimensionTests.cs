using BoomHud.Abstractions.IR;
using FluentAssertions;
using Xunit;

namespace BoomHud.Tests.Unit.IR;

public class DimensionTests
{
    [Theory]
    [InlineData("100px", 100, DimensionUnit.Pixels)]
    [InlineData("50%", 50, DimensionUnit.Percent)]
    [InlineData("1cell", 1, DimensionUnit.Cells)]
    [InlineData("2*", 2, DimensionUnit.Star)]
    [InlineData("*", 1, DimensionUnit.Star)]
    [InlineData("auto", 0, DimensionUnit.Auto)]
    [InlineData("fill", 0, DimensionUnit.Fill)]
    public void Parse_ValidInput_ReturnsCorrectDimension(string input, double expectedValue, DimensionUnit expectedUnit)
    {
        var result = Dimension.Parse(input);

        result.Value.Should().Be(expectedValue);
        result.Unit.Should().Be(expectedUnit);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_EmptyInput_ThrowsArgumentException(string input)
    {
        var act = () => Dimension.Parse(input);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Pixels_CreatesCorrectDimension()
    {
        var dim = Dimension.Pixels(150);

        dim.Value.Should().Be(150);
        dim.Unit.Should().Be(DimensionUnit.Pixels);
        dim.ToString().Should().Be("150px");
    }

    [Fact]
    public void Auto_CreatesCorrectDimension()
    {
        var dim = Dimension.Auto;

        dim.Unit.Should().Be(DimensionUnit.Auto);
        dim.ToString().Should().Be("auto");
    }
}
