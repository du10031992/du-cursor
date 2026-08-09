using System;

namespace MepPanel.Core.Calculations
{
    public static class FireCalculations
    {
        /// <summary>Q = K × √P (Q: L/min, P: bar)</summary>
        public static MepCalculationResult SprinklerFlow(double kFactor, double pressureBar)
        {
            if (pressureBar < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pressureBar));
            }

            double q = kFactor * Math.Sqrt(pressureBar);
            return new MepCalculationResult("Lưu lượng sprinkler", "sprinkler_q", q, "L/min",
                $"K={kFactor}, P={pressureBar} bar (NFPA 13)");
        }

        /// <summary>D = Q / A (Q L/min, A m²)</summary>
        public static MepCalculationResult SprayDensity(double totalFlowLpm, double designAreaM2)
        {
            if (designAreaM2 <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(designAreaM2));
            }

            double d = totalFlowLpm / designAreaM2;
            return new MepCalculationResult("Mật độ phun", "density_area", d, "L/min·m²",
                $"Q={totalFlowLpm} L/min, A={designAreaM2} m²");
        }

        public static MepCalculationResult HydrantTotalFlow(int simultaneousOutlets, double flowPerOutletLs)
        {
            double q = simultaneousOutlets * flowPerOutletLs;
            return new MepCalculationResult("Lưu lượng vòi chữa cháy", "hydrant_q", q, "L/s",
                $"{simultaneousOutlets} vòi × {flowPerOutletLs} L/s");
        }

        public static MepCalculationResult ReserveTankVolume(double flowLpm, double durationMinutes)
        {
            double vLiters = flowLpm * durationMinutes;
            return new MepCalculationResult("Thể tích bồn dự trữ", "tank_volume", vLiters / 1000.0, "m³",
                $"Q={flowLpm} L/min × {durationMinutes} phút");
        }
    }
}
