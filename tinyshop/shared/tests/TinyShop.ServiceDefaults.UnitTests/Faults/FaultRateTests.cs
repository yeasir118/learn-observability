using TinyShop.ServiceDefaults.Abstractions.Faults;

namespace TinyShop.ServiceDefaults.UnitTests.Faults;

public class FaultRateTests
{
    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.5)]
    public void Hits_WithRateZeroOrLess_NeverHits_AndDrawsNothing(double rate)
    {
        var random = new FixedRandom(0.0);

        Assert.False(FaultRate.Hits(rate, random));
        Assert.Equal(0, random.Draws);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.5)]
    public void Hits_WithRateOneOrMore_AlwaysHits_AndDrawsNothing(double rate)
    {
        var random = new FixedRandom(0.999);

        Assert.True(FaultRate.Hits(rate, random));
        Assert.Equal(0, random.Draws);
    }

    [Theory]
    [InlineData(0.0, true)]
    [InlineData(0.199, true)]
    [InlineData(0.2, false)]   // [0, rate) hits, so exactly rate * 100 % of all draws do
    [InlineData(0.999, false)]
    public void Hits_WithRateBetween_HitsWhenTheDrawIsBelowTheRate(double draw, bool expected)
    {
        var random = new FixedRandom(draw);

        Assert.Equal(expected, FaultRate.Hits(0.2, random));
        Assert.Equal(1, random.Draws);
    }

    private sealed class FixedRandom(double value) : IFaultRandom
    {
        public int Draws { get; private set; }

        public double NextDouble()
        {
            Draws++;
            return value;
        }
    }
}
