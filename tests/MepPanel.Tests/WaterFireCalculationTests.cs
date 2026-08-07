using MepPanel.Core.Calculations;
using Xunit;

namespace MepPanel.Tests;

public class WaterFireCalculationTests
{
    [Fact]
    public void Water_velocity_is_in_expected_range_for_demo_inputs()
    {
        var result = WaterCalculations.PipeVelocityFromFlowLs(2.5, 50);
        Assert.Equal("m/s", result.Unit);
        Assert.InRange(result.Value, 1.0, 1.5);
    }

    [Fact]
    public void Fire_sprinkler_flow_matches_k_sqrt_p()
    {
        var result = FireCalculations.SprinklerFlow(80, 1.0);
        Assert.Equal(80.0, result.Value, 3);
        Assert.Equal("L/min", result.Unit);
    }

    [Fact]
    public void Fire_tank_volume_converts_to_cubic_meters()
    {
        var result = FireCalculations.ReserveTankVolume(1500, 60);
        Assert.Equal(90.0, result.Value, 3);
        Assert.Equal("m³", result.Unit);
    }
}
