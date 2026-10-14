using Almacen.DTOs;
using Almacen.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Almacen.Components.Pages;

public partial class CienciaDatos
{
    private bool _cargando = true;
    private bool _refrescando = false;
    private int _tab = 0;
    private bool _datosListos = false;
    private int _lastRenderedTab = -1;

    private StatsInventarioDTO? _statsDevo;
    private StatsInventarioDTO? _statsCons;
    private List<DistribucionCategoriaDTO> _distribucion = new();
    private List<AnomaliaValorDTO> _anomalias = new();
    private List<TendenciaMensualDTO> _tendencias = new();
    private List<RankingResponsableDTO> _ranking = new();
    private List<ProyeccionStockDTO> _proyecciones = new();

    protected override async Task OnInitializedAsync()
    {
        if (!await Seguridad.TienePermisoAsync("ciencia_datos.ver"))
        {
            Navigation.NavigateTo("/");
            return;
        }

        await CargarTodo();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_datosListos) return;
        if (_lastRenderedTab == _tab) return;
        _lastRenderedTab = _tab;

        try
        {
            await Task.Delay(120);

            switch (_tab)
            {
                case 0: await InitChartResumenAsync(); break;
                case 1: await InitChartDistribucionAsync(); break;
                case 3: await InitChartTendenciasAsync(); break;
                case 4: await InitChartRankingAsync(); break;
                case 5: await InitChartProyeccionesAsync(); break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error inicializando chart: " + ex.Message);
        }
    }

    private async Task CambiarTab(int nuevoTab)
    {
        _tab = nuevoTab;
        _lastRenderedTab = -1;
        StateHasChanged();
        await Task.CompletedTask;
    }

    private async Task CargarTodo()
    {
        _cargando = true;
        _datosListos = false;
        _lastRenderedTab = -1;
        StateHasChanged();

        try
        {
            var institucionId = Sesion.InstitucionId;

            var t1 = CDService.ObtenerStatsAsync(institucionId);
            var t2 = CDService.ObtenerDistribucionAsync(institucionId);
            var t3 = CDService.ObtenerAnomaliasAsync(institucionId);
            var t4 = CDService.ObtenerTendenciasAsync(institucionId);
            var t5 = CDService.ObtenerRankingResponsablesAsync(institucionId);
            var t6 = CDService.ObtenerProyeccionesAsync(institucionId);

            await Task.WhenAll(t1, t2, t3, t4, t5, t6);

            var stats = await t1;
            _statsDevo = stats.FirstOrDefault(s => s.TipoBien == "devolutivo");
            _statsCons = stats.FirstOrDefault(s => s.TipoBien == "consumo");
            _distribucion = (await t2).ToList();
            _anomalias = (await t3).ToList();
            _tendencias = (await t4).ToList();
            _ranking = (await t5).ToList();
            _proyecciones = (await t6).ToList();

            _datosListos = true;
        }
        catch (Exception ex)
        {
            Toast.Error("Error al cargar analisis: " + ex.Message);
        }
        finally
        {
            _cargando = false;
            StateHasChanged();
        }
    }

    private async Task InitChartResumenAsync()
    {
        var labels = new[] { "Devolutivos", "Consumo" };
        var valores = new[]
        {
            (double)(_statsDevo?.ValorTotal ?? 0),
            (double)(_statsCons?.ValorTotal ?? 0)
        };
        await JS.InvokeVoidAsync("CDCharts.crearDoughnut", "chart-resumen", labels, valores);

        var labels2 = new[] { "Devolutivos", "Consumo" };
        var valores2 = new[]
        {
            (double)(_statsDevo?.TotalBienes ?? 0),
            (double)(_statsCons?.TotalBienes ?? 0)
        };
        await JS.InvokeVoidAsync("CDCharts.crearBarrasVerticales", "chart-resumen-valores", labels2, valores2);
    }

    private async Task InitChartDistribucionAsync()
    {
        if (!_distribucion.Any()) return;

        var labels = _distribucion.Select(d => d.CategoriaNombre).ToArray();
        var data = _distribucion.Select(d => (double)d.Cantidad).ToArray();

        await JS.InvokeVoidAsync("CDCharts.crearBarrasHorizontales", "chart-distribucion", labels, data);
    }

    private async Task InitChartTendenciasAsync()
    {
        if (!_tendencias.Any()) return;

        var labels = _tendencias.Select(t => t.Mes.ToString("MMM yyyy")).ToArray();
        var data = _tendencias.Select(t => (double)t.ValorEntradas).ToArray();

        await JS.InvokeVoidAsync("CDCharts.crearLinea", "chart-tendencias", labels, data);
    }

    private async Task InitChartRankingAsync()
    {
        if (!_ranking.Any()) return;

        var labels = _ranking.Select(r => r.Funcionario).ToArray();
        var data = _ranking.Select(r => (double)r.TotalBienes).ToArray();

        await JS.InvokeVoidAsync("CDCharts.crearBarrasVerticales", "chart-ranking", labels, data);
    }

    private async Task InitChartProyeccionesAsync()
    {
        if (!_proyecciones.Any()) return;

        var labels = _proyecciones.Select(p => p.Nombre).ToArray();
        var stock = _proyecciones.Select(p => (double)p.StockActual).ToArray();
        var minimo = _proyecciones.Select(p => (double)p.StockMinimo).ToArray();

        await JS.InvokeVoidAsync("CDCharts.crearBarrasComparativas", "chart-proyecciones", labels, stock, minimo);
    }

    private async Task RefrescarVistas()
    {
        _refrescando = true;
        StateHasChanged();

        try
        {
            await CDService.RefrescarVistasAsync();
            await CargarTodo();

            await Auditoria.RegistrarExportacionAsync(
                "ciencia_datos",
                "Refresco manual de vistas materializadas"
            );

            Toast.Exito("Vistas actualizadas con datos frescos");
        }
        catch (Exception ex)
        {
            Toast.Error("Error al refrescar: " + ex.Message);
        }
        finally
        {
            _refrescando = false;
            StateHasChanged();
        }
    }

    private static string FormatearMoneda(decimal valor)
        => valor.ToString("C0", new System.Globalization.CultureInfo("es-CO"));

    private static string ClasificarCv(decimal cv) => cv switch
    {
        < 25 => "badge-ok",
        < 50 => "badge-info",
        _ => "badge-warn"
    };

    private static string ClasificarAnomalia(string clasificacion) => clasificacion switch
    {
        "ANOMALO" => "badge-danger",
        "REVISAR" => "badge-warn",
        _ => "badge-neutral"
    };

    private static string ClasificarProyeccion(string estado) => estado switch
    {
        "CRITICO" => "badge-danger",
        "ALERTA" => "badge-warn",
        _ => "badge-ok"
    };
}