using System.Globalization;

namespace Almacen.Helpers
{
    /// <summary>
    /// Formato oficial de dinero del sistema.
    /// Regla visual: "$12.000" — símbolo pegado, miles con punto, decimales con coma.
    /// </summary>
    public static class Moneda
    {
        public static readonly CultureInfo Co = CrearCultura();

        private static CultureInfo CrearCultura()
        {
            var c = (CultureInfo)CultureInfo.GetCultureInfo("es-CO").Clone();

            // Moneda: $12.000 (pegado, sin espacio entre $ y número)
            c.NumberFormat.CurrencySymbol = "$";
            c.NumberFormat.CurrencyGroupSeparator = ".";
            c.NumberFormat.CurrencyDecimalSeparator = ",";
            c.NumberFormat.CurrencyPositivePattern = 0; // $n
            c.NumberFormat.CurrencyNegativePattern = 1; // -$n

            // Números planos: 1.234,56
            c.NumberFormat.NumberGroupSeparator = ".";
            c.NumberFormat.NumberDecimalSeparator = ",";

            return c;
        }

        /// <summary>Valor sin centavos → "$12.000"</summary>
        public static string Entero(decimal v) => v.ToString("C0", Co);

        /// <summary>Valor contable con centavos → "$12.345,67" (NO redondea)</summary>
        public static string Contable(decimal v) => v.ToString("C2", Co);

        /// <summary>CPP con 4 decimales, sin símbolo → "1.234,5678"</summary>
        public static string Cpp(decimal v) => v.ToString("N4", Co);

        /// <summary>Cantidades / stock → "12,50"</summary>
        public static string Cantidad(decimal v) => v.ToString("N2", Co);

        /// <summary>Enteros sin moneda → "1.234"</summary>
        public static string Numero(decimal v) => v.ToString("N0", Co);

        /// <summary>KPI compacto con $ → "$12,0M" · "$1,5B" · "$500K"</summary>
        public static string Kpi(decimal valor)
        {
            var abs = Math.Abs(valor);
            var signo = valor < 0 ? "-" : "";

            if (abs >= 1_000_000_000m)
                return $"{signo}${(abs / 1_000_000_000m).ToString("N1", Co)}B";

            if (abs >= 1_000_000m)
                return $"{signo}${(abs / 1_000_000m).ToString("N1", Co)}M";

            if (abs >= 1_000m)
                return $"{signo}${(abs / 1_000m).ToString("N0", Co)}K";

            return $"{signo}{abs.ToString("C0", Co)}";
        }

        // ─── Sobrecargas nullable ───
        public static string Entero(decimal? v) => v.HasValue ? Entero(v.Value) : "—";
        public static string Contable(decimal? v) => v.HasValue ? Contable(v.Value) : "—";
        public static string Cpp(decimal? v) => v.HasValue ? Cpp(v.Value) : "—";
        public static string Cantidad(decimal? v) => v.HasValue ? Cantidad(v.Value) : "—";
    }
}