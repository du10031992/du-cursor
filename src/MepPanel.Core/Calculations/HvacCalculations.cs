using System;

namespace MepPanel.Core.Calculations
{
    public static class HvacCalculations
    {
        /// <summary>Q_s = m × cp × ΔT</summary>
        public static MepCalculationResult SensibleLoad(double massFlowKgPerS, double deltaTK)
        {
            const double cp = 1005.0;
            double q = massFlowKgPerS * cp * deltaTK;
            return new MepCalculationResult("Tải nhiệt hiển", "sensible_load", q, "W",
                $"m={massFlowKgPerS} kg/s, ΔT={deltaTK} K");
        }

        /// <summary>L = Q / (ρ × cp × ΔT)</summary>
        public static MepCalculationResult AirFlowFromCoolingLoad(double coolingLoadW, double deltaTK)
        {
            if (deltaTK <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTK));
            }

            const double rho = 1.2;
            const double cp = 1005.0;
            double l = coolingLoadW / (rho * cp * deltaTK);
            return new MepCalculationResult("Lưu lượng gió", "air_flow_from_Q", l, "m³/s",
                $"Q={coolingLoadW} W, ΔT={deltaTK} K (~{l * 3600:0} m³/h)");
        }

        public static MepCalculationResult DuctVelocity(double airFlowM3PerS, double widthMm, double heightMm)
        {
            if (widthMm <= 0 || heightMm <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(widthMm));
            }

            double area = (widthMm / 1000.0) * (heightMm / 1000.0);
            double v = airFlowM3PerS / area;
            string note = v >= 2.5 && v <= 10 ? "Trong khoảng SMACNA" : "Kiểm tra tiết diện / tốc độ";
            return new MepCalculationResult("Tốc độ gió trong ống", "duct_velocity", v, "m/s", note);
        }

        public static MepCalculationResult CoolingTons(double coolingKw)
        {
            const double kwPerTon = 3.517;
            return new MepCalculationResult("Tải lạnh", "cooling_ton", coolingKw / kwPerTon, "RT",
                $"{coolingKw} kW");
        }
    }
}
