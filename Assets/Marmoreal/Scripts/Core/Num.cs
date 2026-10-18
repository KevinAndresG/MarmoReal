using System;

namespace Marmoreal
{
    /// <summary>Formateo compacto de números grandes (1.25K, 3.4M, 7.0e42...).</summary>
    public static class Num
    {
        static readonly string[] Suffix = { "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc", "Ud", "Dd", "Td" };

        public static string F(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "∞";
            if (v < 0) return "-" + F(-v);
            if (v < 1000)
                return v < 10 && v - Math.Floor(v) > 0.05 ? v.ToString("0.0") : Math.Floor(v).ToString("0");
            int e = (int)Math.Floor(Math.Log10(v) / 3.0);
            if (e < Suffix.Length)
            {
                double m = v / Math.Pow(1000, e);
                if (m >= 999.995) { m /= 1000; e++; if (e >= Suffix.Length) return v.ToString("0.00e0"); }
                return m.ToString(m < 10 ? "0.00" : m < 100 ? "0.0" : "0") + Suffix[e];
            }
            return v.ToString("0.00e0");
        }

        public static string Pct(float v) => (v * 100f).ToString("0") + "%";

        public static string Roman(int n)
        {
            if (n <= 0) return "0";
            if (n > 3999) return n.ToString();
            string[] th = { "", "M", "MM", "MMM" };
            string[] h = { "", "C", "CC", "CCC", "CD", "D", "DC", "DCC", "DCCC", "CM" };
            string[] t = { "", "X", "XX", "XXX", "XL", "L", "LX", "LXX", "LXXX", "XC" };
            string[] o = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX" };
            return th[n / 1000] + h[n % 1000 / 100] + t[n % 100 / 10] + o[n % 10];
        }

        public static string Time(double seconds)
        {
            var ts = TimeSpan.FromSeconds(seconds);
            if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            if (ts.TotalMinutes >= 1) return $"{ts.Minutes}m {ts.Seconds}s";
            return $"{ts.Seconds}s";
        }
    }
}
