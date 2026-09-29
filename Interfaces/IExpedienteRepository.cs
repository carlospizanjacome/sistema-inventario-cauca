using Almacen.DTOs;

namespace Almacen.Interfaces;

public interface IExpedienteRepository
{
    /// <summary>
    /// Obtiene el expediente 360° de un bien (datos + movimientos + depreciación + tomas físicas).
    /// Aplica filtro multi-institución. Retorna null si el bien no existe o no pertenece a la IE del usuario.
    /// </summary>
    Task<ExpedienteBienDTO?> ObtenerExpedienteAsync(int bienId);
}