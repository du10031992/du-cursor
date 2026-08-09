using System.Collections.Generic;

namespace MepPanel.Core.Standards
{
    public static class MepFormulaCatalog
    {
        public static IReadOnlyList<MepFormulaInfo> GetBySystem(MepSystemKind system)
        {
            switch (system)
            {
                case MepSystemKind.Electrical: return Electrical;
                case MepSystemKind.Water: return Water;
                case MepSystemKind.Fire: return Fire;
                case MepSystemKind.Hvac: return Hvac;
                default: return new MepFormulaInfo[0];
            }
        }

        public static readonly MepFormulaInfo[] Electrical =
        {
            new MepFormulaInfo("I_3phase", "Dòng điện 3 pha", "I = P / (√3 × U × cosφ)", "P(W), U(V), cosφ", "A", "TCVN 9207 / IEC 60364"),
            new MepFormulaInfo("I_1phase", "Dòng điện 1 pha", "I = P / (U × cosφ)", "P(W), U(V)", "A", "TCVN 9207"),
            new MepFormulaInfo("dU_cable", "Sụt áp dây đồng", "ΔU% = (2 × I × L × ρ) / (S × U) × 100", "I(A), L(m), S(mm²), ρ≈0.0175 Ω·mm²/m", "%", "TCVN 9207 — thường ≤ 3%"),
            new MepFormulaInfo("P_calc", "Công suất tác dụng", "P = √3 × U × I × cosφ", "U(V), I(A)", "W", "IEC 60364"),
            new MepFormulaInfo("k_factor", "Hệ số sử dụng", "I_thiết_kế = I_danh_định × Ks × Ka", "Ks: hệ số đồng thời", "A", "Thực tế thi công tủ điện")
        };

        public static readonly MepFormulaInfo[] Water =
        {
            new MepFormulaInfo("Q_flow", "Lưu lượng nước", "Q = A × v = (π × D² / 4) × v", "Q(m³/s), D(m), v(m/s)", "m³/s, chuyển l/s: ×1000", "TCVN 4513"),
            new MepFormulaInfo("hazen_williams", "Hazen-Williams (ống tròn)", "v = 0.849 × C × R^0.63 × S^0.54", "C: hệ số ống, R(m), S=m/m", "m/s", "Thực tế cấp nước"),
            new MepFormulaInfo("head_loss", "Tổn thất áp lực Darcy-Weisbach", "hf = f × (L/D) × (v²/2g)", "f: hệ số ma sát, g=9.81", "m cột nước", "TCVN 4513 / ASHRAE"),
            new MepFormulaInfo("velocity_check", "Kiểm tra tốc độ ống", "v = Q / A", "Q(m³/s), A(m²)", "m/s — cấp 1–2.5; thoát 0.6–1.2", "QCVN 01 / TCVN 4474"),
            new MepFormulaInfo("pump_head", "Cột áp bơm", "H = (P_out - P_in)/(ρg) + Δz + hf", "ρ=1000 kg/m³", "m", "Thi công trạm bơm")
        };

        public static readonly MepFormulaInfo[] Fire =
        {
            new MepFormulaInfo("sprinkler_q", "Lưu lượng sprinkler (NFPA)", "Q = K × √P", "K: hệ số phun (L/min/√bar), P(bar)", "L/min", "NFPA 13 / TCVN 6160"),
            new MepFormulaInfo("density_area", "Mật độ phun", "D = Q / A", "D(L/min·m²), A(m²)", "L/min·m²", "QCVN 06 — vùng thiết kế"),
            new MepFormulaInfo("hydrant_q", "Lưu lượng vòi chữa cháy", "Q = n × q_vòi", "n: số vòi đồng thời", "L/s", "TCVN 6160 / NFPA 14"),
            new MepFormulaInfo("pipe_friction_fire", "Tổn thất áp PCCC", "ΔP = friction + elevation + fittings", "Hệ số K phụ kiện", "bar / kPa", "Thi công thực tế"),
            new MepFormulaInfo("tank_volume", "Thể tích bồn dự trữ", "V = Q × t / 60", "Q(L/min), t(phút cấp)", "L, m³", "QCVN 06")
        };

        public static readonly MepFormulaInfo[] Hvac =
        {
            new MepFormulaInfo("sensible_load", "Tải nhiệt hiển", "Q_s = m × cp × ΔT", "m(kg/s), cp=1005 J/kg·K", "W", "ASHRAE / TCVN 9391"),
            new MepFormulaInfo("air_flow_from_Q", "Lưu lượng gió theo tải", "L = Q / (ρ × cp × ΔT)", "ρ≈1.2 kg/m³", "m³/s", "TCVN 5687"),
            new MepFormulaInfo("duct_velocity", "Tốc độ gió trong ống", "v = L / A", "A = W × H (m²)", "m/s — thường 2.5–8", "SMACNA"),
            new MepFormulaInfo("duct_size", "Tiết diện ống gió", "A = L / v", "Chọn W×H thương mại", "mm", "TCVN 9391"),
            new MepFormulaInfo("cooling_ton", "Tải lạnh (RT)", "1 RT = 3.517 kW", "Q(kW) / 3.517", "RT", "Thi công VN")
        };
    }
}
