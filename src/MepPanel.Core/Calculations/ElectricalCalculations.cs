using System;

namespace MepPanel.Core.Calculations
{
    public static class ElectricalCalculations
    {
        /// <summary>I = P / (√3 × U × cosφ)</summary>
        public static MepCalculationResult ThreePhaseCurrent(double powerW, double voltageV, double powerFactor)
        {
            if (voltageV <= 0 || powerFactor <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(voltageV), "U và cosφ phải > 0.");
            }

            double i = powerW / (Math.Sqrt(3) * voltageV * powerFactor);
            return new MepCalculationResult(
                "Dòng điện 3 pha",
                "I_3phase",
                i,
                "A",
                $"P={powerW}W, U={voltageV}V, cosφ={powerFactor}");
        }

        /// <summary>ΔU% = (2 × I × L × ρ) / (S × U) × 100 (1 pha đơn giản)</summary>
        public static MepCalculationResult VoltageDropPercent(
            double currentA,
            double lengthM,
            double sectionMm2,
            double voltageV,
            double resistivityOhmMm2PerM = 0.0175)
        {
            if (sectionMm2 <= 0 || voltageV <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sectionMm2), "S và U phải > 0.");
            }

            double delta = (2 * currentA * lengthM * resistivityOhmMm2PerM) / (sectionMm2 * voltageV) * 100;
            string note = delta <= 3 ? "Đạt (≤3% TCVN 9207)" : "Vượt 3% — tăng tiết diện";
            return new MepCalculationResult("Sụt áp dây", "dU_cable", delta, "%", note);
        }

        public static MepCalculationResult SinglePhaseCurrent(double powerW, double voltageV, double powerFactor)
        {
            if (voltageV <= 0 || powerFactor <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(voltageV));
            }

            double i = powerW / (voltageV * powerFactor);
            return new MepCalculationResult("Dòng điện 1 pha", "I_1phase", i, "A",
                $"P={powerW}W, U={voltageV}V");
        }
    }
}
