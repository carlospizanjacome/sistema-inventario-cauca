namespace Almacen.Helpers;

public static class FiltroInstitucion
{
    public static (string Sql, object? Parametros) Construir(
        bool esSuperAdmin,
        int institucionId,
        string? tabla = null,
        string columna = "institucion_id")
    {
        if (esSuperAdmin || institucionId <= 0)
            return (string.Empty, null);

        var prefijo = string.IsNullOrEmpty(tabla) ? string.Empty : $"{tabla}.";
        return ($" AND {prefijo}{columna} = @InstitucionId", new { InstitucionId = institucionId });
    }
}