using Npgsql;
using Almacen.Services;

namespace Almacen.Helpers;

public static class ErrorHelper
{
    public static void Mostrar(ToastService toast, Exception ex, string objeto, string accion = "procesar")
    {
        if (ex is PostgresException pg)
        {
            switch (pg.SqlState)
            {
                case "23503":
                    toast.Warning($"No se puede {accion} {objeto}: tiene movimientos o procesos registrados en el sistema. Para darlo de baja, use el módulo correspondiente (Salidas / Bajas).");
                    return;

                case "23505":
                    toast.Warning($"No se puede {accion} {objeto}: ya existe un registro con ese código o identificador.");
                    return;

                case "23502":
                    toast.Warning($"No se puede {accion} {objeto}: hay campos obligatorios sin completar.");
                    return;

                case "23514":
                    toast.Warning($"No se puede {accion} {objeto}: los datos no cumplen las validaciones.");
                    return;

                case "22001":
                    toast.Warning($"No se puede {accion} {objeto}: algún texto es demasiado largo para el campo.");
                    return;

                case "42P01":
                    toast.Error("Error interno: tabla no encontrada. Contacte al administrador.");
                    return;

                case "42703":
                    toast.Error("Error interno: columna no encontrada. Contacte al administrador.");
                    return;
            }
        }

        var msg = ex.Message ?? "";

        if (msg.Contains("23503") || msg.Contains("llave foránea") || msg.Contains("foreign key"))
        {
            toast.Warning($"No se puede {accion} {objeto}: tiene movimientos o procesos registrados en el sistema.");
            return;
        }

        if (msg.Contains("23505") || msg.Contains("duplicate key") || msg.Contains("unique constraint"))
        {
            toast.Warning($"No se puede {accion} {objeto}: ya existe un registro con ese código o identificador.");
            return;
        }

        toast.Error($"Error al {accion} {objeto}: {msg}");
    }
}