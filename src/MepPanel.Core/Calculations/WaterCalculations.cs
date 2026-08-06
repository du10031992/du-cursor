using System;

namespace MepPanel.Core.Calculations
{
    public static class WaterCalculations
    {
        /// <summary>v = Q / A, Q in L/s converted internally</summary>
        public static MepCalculationResult PipeVelocityFromFlowLs(double flowLs, double diameterMm)
        {
            if (diameterMm <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(diameterMm));
            }

            double qM3s = flowLs / 1000.0;
            double area = Math.PI * Math.Pow(diameterMm / 1000.0 / 2.0, 2);
            double v = qM3s / area;
            string note = v >= 0.6 && v <= 2.5 ? "Trong khoảng khuyến nghị cấp nước" : "Kiểm tra TCVN 4513 / ASHRAE";
            return new MepCalculationResult("Tốc độ nước trong ống", "velocity_check", v, "m/s",
                $"Q={flowLs} L/s, DN={diameterMm}mm — {note}");
        }

        /// <summary>Q = A × v → L/s</summary>
        public static MepCalculationResult FlowFromVelocity(double velocityMs, double diameterMm)
        {
            if (diameterMm <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(diameterMm));
            }

            double area = Math.PI * Math.Pow(diameterMm / 1000.0 / 2.0, 2);
            double qLs = area * velocityMs * 1000.0;
            return new MepCalculationResult("Lưu lượng nước", "Q_flow", qLs, "L/s",
                $"v={velocityMs} m/s, DN={diameterMm}mm");
        }

        /// <summary>hf = f × (L/D) × (v²/2g)</summary>
        public static MepCalculationResult DarcyHeadLoss(
            double frictionFactor,
            double lengthM,
            double diameterMm,
            double velocityMs)
        {
            if (diameterMm <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(diameterMm));
            }

            double d = diameterMm / 1000.0;
            const double g = 9.81;
            double hf = frictionFactor * (lengthM / d) * (velocityMs * velocityMs / (2 * g));
            return new MepCalculationResult("Tổn thất cột nước", "head_loss", hf, "m",
                $"f={frictionFactor}, L={lengthM}m, DN={diameterMm}mm");
        }
    }
}
