using Almacen.DTOs;
using Almacen.Interfaces;

namespace Almacen.Services;

public class DepreciacionService
{
    private readonly IDepreciacionRepository _repo;

    public DepreciacionService(IDepreciacionRepository repo)
    {
        _repo = repo;
    }

    /// <summary>
    /// Calcula la depreciación para todos los bienes devolutivos activos
    /// desde el mes siguiente al último cálculo hasta el mes actual.
    /// </summary>
    public async Task<ResultadoDepreciacionDTO> CalcularDepreciacionAsync()
    {
        var resultado = new ResultadoDepreciacionDTO
        {
            FechaEjecucion = DateTime.Now
        };

        var bienes = (await _repo.ObtenerResumenBienesAsync()).ToList();
        resultado.TotalBienes = bienes.Count;

        var hoy = DateTime.Today;

        foreach (var bien in bienes)
        {
            try
            {
                if (bien.VidaUtilMeses is null || bien.VidaUtilMeses <= 0)
                {
                    resultado.BienesOmitidos++;
                    resultado.Advertencias.Add(
                        $"Bien '{bien.Codigo}' sin vida útil asignada.");
                    continue;
                }

                if (bien.FechaAdquisicion is null)
                {
                    resultado.BienesOmitidos++;
                    continue;
                }

                // Determinar desde qué mes calcular
                var ultimo = await _repo.ObtenerUltimoPeriodoAsync(bien.BienId);
                DateTime desde;

                if (ultimo is null)
                {
                    // Primera vez: empezar desde el mes de adquisición
                    desde = new DateTime(
                        bien.FechaAdquisicion.Value.Year,
                        bien.FechaAdquisicion.Value.Month,
                        1);
                }
                else
                {
                    // Empezar desde el mes siguiente al último calculado
                    desde = new DateTime(ultimo.PeriodoAnio, ultimo.PeriodoMes, 1)
                        .AddMonths(1);
                }

                // Valor a depreciar y cuota mensual
                var valorADepreciar = bien.ValorInicial - bien.ValorResidual;
                var cuotaMensual = Math.Round(valorADepreciar / bien.VidaUtilMeses.Value, 2);

                var registros = new List<DepreciacionDTO>();
                var depreciacionAcumulada = ultimo?.DepreciacionAcumulada ?? 0;
                var fechaActual = desde;

                while (fechaActual <= hoy)
                {
                    // Si ya se depreció al 100%, detenerse
                    if (depreciacionAcumulada >= valorADepreciar)
                    {
                        break;
                    }

                    // Cuota del mes (no exceder el total)
                    var cuotaEsteMes = Math.Min(cuotaMensual, valorADepreciar - depreciacionAcumulada);
                    depreciacionAcumulada += cuotaEsteMes;

                    var valorNeto = bien.ValorInicial - depreciacionAcumulada;

                    registros.Add(new DepreciacionDTO
                    {
                        BienId = bien.BienId,
                        PeriodoAnio = fechaActual.Year,
                        PeriodoMes = fechaActual.Month,
                        ValorInicial = bien.ValorInicial,
                        ValorResidual = bien.ValorResidual,
                        DepreciacionMes = cuotaEsteMes,
                        DepreciacionAcumulada = depreciacionAcumulada,
                        ValorNeto = valorNeto
                    });

                    fechaActual = fechaActual.AddMonths(1);
                }

                if (registros.Count > 0)
                {
                    await _repo.GuardarDepreciacionAsync(bien.BienId, registros);
                    await _repo.ActualizarCamposBienAsync(
                        bien.BienId,
                        depreciacionAcumulada,
                        bien.ValorInicial - depreciacionAcumulada);

                    resultado.BienesCalculados++;
                    resultado.DepreciacionTotalMes += registros.Sum(r => r.DepreciacionMes);
                }
                else
                {
                    resultado.BienesOmitidos++;
                }
            }
            catch (Exception ex)
            {
                resultado.Advertencias.Add(
                    $"Error en bien '{bien.Codigo}': {ex.Message}");
                resultado.BienesOmitidos++;
            }
        }

        return resultado;
    }
}