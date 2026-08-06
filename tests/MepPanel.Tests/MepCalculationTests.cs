using MepPanel.Core.Calculations;
using Xunit;

namespace MepPanel.Tests;

public class MepCalculationTests
{
    [Fact]
    public void ThreePhaseCurrent_15kW_380V()
    {
        var r = ElectricalCalculations.ThreePhaseCurrent(15000, 380, 0.85);
        Assert.InRange(r.Value, 26, 27);
        Assert.Equal("A", r.Unit);
    }

    [Fact]
    public void VoltageDrop_WithinLimit()
    {
        var r = ElectricalCalculations.VoltageDropPercent(32, 25, 6, 220);
        Assert.True(r.Value > 0);
    }

    [Fact]
    public void WaterVelocity_2p5Ls_DN50()
    {
        var r = WaterCalculations.PipeVelocityFromFlowLs(2.5, 50);
        Assert.InRange(r.Value, 1.0, 1.5);
    }

    [Fact]
    public void SprinklerFlow_K80_P1bar()
    {
        var r = FireCalculations.SprinklerFlow(80, 1.0);
        Assert.InRange(r.Value, 79, 81);
    }

    [Fact]
    public void HvacAirFlow_12kW()
    {
        var r = HvacCalculations.AirFlowFromCoolingLoad(12000, 8);
        Assert.True(r.Value > 1.0);
    }

    [Fact]
    public void CoolingTons_35kW()
    {
        var r = HvacCalculations.CoolingTons(35);
        Assert.InRange(r.Value, 9.9, 10.0);
    }
}
