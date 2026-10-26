using OMX.Mods.IntelligentSpeedAssistance;
using Xunit;

namespace Isa.Tests;

public sealed class ControllerTests
{
    [Fact]
    public void ThrottleTapersAtTheLimitAndRecoversGradually()
    {
        var controller = new SpeedAssistanceController(new() { ApproachBandKmh = 2f });
        Assert.Equal(1f, controller.Update(20f, 30f, 0.1));
        Assert.Equal(0.5f, controller.Update(29f, 30f, 0.1));
        Assert.Equal(0f, controller.Update(30f, 30f, 0.1));
        Assert.Equal(0f, controller.Update(35f, 30f, 0.1));
        Assert.Equal(0.2f, controller.Update(35f, 50f, 0.1));
        controller.Reset();
        Assert.Equal(1f, controller.ThrottleLimit);
    }

    [Theory]
    [InlineData("mph")]
    [InlineData("km/h")]
    public void MphBusOnMetricMapUsesTheSamePhysicalLimit(string displayUnit)
    {
        var controller = new SpeedAssistanceController(new() { SpeedUnit = displayUnit });
        // A bus travelling at 30 mph is below a 50 km/h road limit; 32 mph is above it.
        Assert.Equal(1f, controller.Update(30f * 1.609344f, 50f, 0.1));
        Assert.Equal(0f, controller.Update(32f * 1.609344f, 50f, 0.1));
    }

    [Fact]
    public void DisabledOrUnavailableInputsReleaseTheCap()
    {
        var disabled = new SpeedAssistanceController(new() { Enabled = false });
        Assert.Equal(1f, disabled.Update(90f, 30f, 0.1));
        var controller = new SpeedAssistanceController(new());
        Assert.Equal(0f, controller.Update(90f, 30f, 0.1));
        Assert.Equal(1f, controller.Update(90f, null, 0.1));
        Assert.Equal(1f, controller.Update(float.NaN, 30f, 0.1));
        Assert.Equal(1f, controller.Update(90f, -1f, 0.1));
        Assert.Equal(1f, controller.Update(90f, 30f, double.NaN));
        Assert.Equal(1f, controller.Update(90f, 30f, -1));
    }

    [Fact]
    public void GraceExpiresAtFiveSecondsAndRestartsWhenRoadIsFound()
    {
        var memory = new RoadLimitMemory(5);
        Assert.Null(memory.Update(null, 1));
        Assert.Equal(30f, memory.Update(30f, 0.1));
        Assert.Equal(30f, memory.Update(null, 4.9));
        Assert.Null(memory.Update(null, 0.1));
        Assert.Equal(50f, memory.Update(50f, 0.1));
        Assert.Equal(50f, memory.Update(null, 4));
        Assert.Equal(30f, memory.Update(30f, 0.1));
        Assert.Equal(30f, memory.Update(null, 4));
        memory.Reset();
        Assert.Null(memory.Update(null, 0.1));
    }

    [Fact]
    public void InvalidDataClearsRoadMemoryAndZeroGraceExpiresImmediately()
    {
        var memory = new RoadLimitMemory(5);
        memory.Update(30f, 0.1);
        Assert.Equal(-1f, memory.Update(-1f, 0.1));
        Assert.Null(memory.Update(null, 0.1));
        memory.Update(30f, 0.1);
        Assert.Null(memory.Update(null, double.NaN));
        Assert.Null(memory.Update(null, 0.1));
        var immediate = new RoadLimitMemory(0);
        immediate.Update(30f, 0.1);
        Assert.Null(immediate.Update(null, 0));
    }

    [Theory]
    [InlineData("MPH")]
    [InlineData("knots")]
    public void UnsupportedUnitsAreRejected(string unit) =>
        Assert.Throws<ArgumentException>(() => new IsaConfiguration { SpeedUnit = unit }.Validate());

    [Theory]
    [InlineData(-1f)]
    [InlineData(61f)]
    [InlineData(float.NaN)]
    public void InvalidGraceIsRejected(float seconds) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new IsaConfiguration { RoadDataGraceSeconds = seconds }.Validate());

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void InvalidTaperAndRecoveryAreRejected(float value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new IsaConfiguration { ApproachBandKmh = value }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new IsaConfiguration { ThrottleRecoveryPerSecond = value }.Validate());
    }
}
