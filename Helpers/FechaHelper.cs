namespace RawSuplementos.Api.Helpers
{
    public static class FechaHelper
    {
        private static readonly TimeZoneInfo ZonaCostaRica =
            TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows()
                    ? "Central America Standard Time"
                    : "America/Costa_Rica"
            );

        // ==========================================
        // PARA GUARDAR EN POSTGRESQL
        // ==========================================

        public static DateTime AhoraUtc()
        {
            return DateTime.UtcNow;
        }

        // ==========================================
        // HORA ACTUAL DE COSTA RICA
        // Solo para mostrar / lógica local
        // ==========================================

        public static DateTime AhoraCostaRica()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                ZonaCostaRica
            );
        }

        public static DateTime HoyCostaRica()
        {
            return AhoraCostaRica().Date;
        }

        // ==========================================
        // INICIO DEL DÍA CR CONVERTIDO A UTC
        // Ideal para consultas PostgreSQL
        // ==========================================

        public static DateTime InicioHoyCostaRicaUtc()
        {
            var hoyCostaRica =
                AhoraCostaRica().Date;

            var fechaLocal = DateTime.SpecifyKind(
                hoyCostaRica,
                DateTimeKind.Unspecified
            );

            return TimeZoneInfo.ConvertTimeToUtc(
                fechaLocal,
                ZonaCostaRica
            );
        }

        public static DateTime InicioMananaCostaRicaUtc()
        {
            var mananaCostaRica =
                AhoraCostaRica().Date.AddDays(1);

            var fechaLocal = DateTime.SpecifyKind(
                mananaCostaRica,
                DateTimeKind.Unspecified
            );

            return TimeZoneInfo.ConvertTimeToUtc(
                fechaLocal,
                ZonaCostaRica
            );
        }

        // ==========================================
        // FECHA CR -> UTC
        // Para vencimientos
        // ==========================================

        public static DateTime CostaRicaAUtc(
            DateTime fechaCostaRica)
        {
            var local = DateTime.SpecifyKind(
                fechaCostaRica,
                DateTimeKind.Unspecified
            );

            return TimeZoneInfo.ConvertTimeToUtc(
                local,
                ZonaCostaRica
            );
        }

        // ==========================================
        // UTC -> COSTA RICA
        // Para mostrar
        // ==========================================

        public static DateTime UtcACostaRica(
            DateTime fechaUtc)
        {
            if (fechaUtc.Kind != DateTimeKind.Utc)
            {
                fechaUtc = DateTime.SpecifyKind(
                    fechaUtc,
                    DateTimeKind.Utc
                );
            }

            return TimeZoneInfo.ConvertTimeFromUtc(
                fechaUtc,
                ZonaCostaRica
            );
        }
    }
}