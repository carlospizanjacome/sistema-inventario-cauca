using Almacen.DTOs;
using Almacen.Helpers;
using Almacen.Interfaces;
using Almacen.Services;
using Dapper;
using Npgsql;

namespace Almacen.Repositories;

public class ExpedienteRepository : IExpedienteRepository
{
    private readonly string _cs;
    private readonly UsuarioSesionService _sesion;

    public ExpedienteRepository(IConfiguration cfg, UsuarioSesionService sesion)
    {
        _cs = cfg.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta DefaultConnection.");
        _sesion = sesion;
    }

    public async Task<ExpedienteBienDTO?> ObtenerExpedienteAsync(int bienId)
    {
        using var cn = new NpgsqlConnection(_cs);

        var (filtro, param) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "b");

        // ═══════════════════════════════════════════════════════════
        // 1. Datos principales del bien (con joins completos)
        // ═══════════════════════════════════════════════════════════
        var sqlBien = $@"
            SELECT
                b.id,
                b.codigo,
                b.nombre,
                b.descripcion,
                b.tipo_bien              AS TipoBien,
                b.estado_fisico          AS EstadoFisico,
                b.activo,
                b.codigo_qr              AS CodigoQr,
                b.marca,
                b.modelo,
                b.serie,
                b.cantidad,

                c.nombre                 AS CategoriaNombre,
                c.codigo_cgn             AS CodigoCgn,

                vu.descripcion           AS VidaUtilDescripcion,
                vu.vida_util_meses       AS VidaUtilMeses,

                a.nombre                 AS AulaNombre,
                bl.nombre                AS BloqueNombre,
                s.nombre                 AS SedeNombre,
                i.nombre                 AS InstitucionNombre,

                f.nombre_completo        AS FuncionarioNombre,
                f.cedula                 AS FuncionarioCedula,
                f.cargo                  AS FuncionarioCargo,

                b.valor_adquisicion      AS ValorAdquisicion,
                b.valor_residual         AS ValorResidual,
                b.depreciacion_acumulada AS DepreciacionAcumulada,
                b.valor_neto             AS ValorNeto,
                b.fecha_adquisicion::timestamp AS FechaAdquisicion,
                b.fecha_ultimo_calculo   AS FechaUltimoCalculo,
                b.created_at             AS CreatedAt,
                b.updated_at             AS UpdatedAt
            FROM bienes b
            LEFT JOIN categorias c   ON c.id = b.categoria_id
            LEFT JOIN catalogo_cgn vu ON vu.id = b.vida_util_id
            LEFT JOIN aulas a        ON a.id = b.aula_id
            LEFT JOIN bloques bl     ON bl.id = a.bloque_id
            LEFT JOIN sedes s        ON s.id = bl.sede_id
            LEFT JOIN instituciones i ON i.id = b.institucion_id
            LEFT JOIN funcionarios f ON f.id = b.funcionario_id
            WHERE b.id = @Id {filtro};";

        var expediente = await cn.QueryFirstOrDefaultAsync<ExpedienteBienDTO>(
            sqlBien,
            new { Id = bienId, InstitucionId = _sesion.InstitucionId });

        if (expediente is null) return null;

        // ═══════════════════════════════════════════════════════════
        // 2. Entradas
        // ═══════════════════════════════════════════════════════════
        const string sqlEntradas = @"
            SELECT
                e.id,
                e.tipo_fuente             AS TipoFuente,
                e.numero_factura          AS NumeroFactura,
                e.fecha_entrada::timestamp AS FechaEntrada,
                e.valor,
                p.nombre                  AS ProveedorNombre,
                f.nombre_completo         AS FuncionarioNombre,
                e.observaciones
            FROM entradas e
            LEFT JOIN proveedores p    ON p.id = e.proveedor_id
            LEFT JOIN funcionarios f   ON f.id = e.funcionario_recibe_id
            WHERE e.bien_id = @Id
            ORDER BY e.fecha_entrada DESC, e.id DESC;";

        expediente.Entradas = (await cn.QueryAsync<ExpedienteEntradaDTO>(
            sqlEntradas, new { Id = bienId })).ToList();

        // ═══════════════════════════════════════════════════════════
        // 3. Salidas / Bajas
        // ═══════════════════════════════════════════════════════════
        const string sqlSalidas = @"
            SELECT
                s.id,
                s.tipo_baja              AS TipoBaja,
                s.motivo,
                s.fecha_salida::timestamp AS FechaSalida,
                s.numero_acta_comite     AS NumeroActaComite,
                s.numero_denuncia        AS NumeroDenuncia,
                s.valor_salida           AS ValorSalida,
                f.nombre_completo        AS FuncionarioApruebaNombre,
                s.observaciones
            FROM salidas s
            LEFT JOIN funcionarios f ON f.id = s.funcionario_aprueba_id
            WHERE s.bien_id = @Id
            ORDER BY s.fecha_salida DESC, s.id DESC;";

        expediente.Salidas = (await cn.QueryAsync<ExpedienteSalidaDTO>(
            sqlSalidas, new { Id = bienId })).ToList();

        // ═══════════════════════════════════════════════════════════
        // 4. Traslados
        // ═══════════════════════════════════════════════════════════
        const string sqlTraslados = @"
            SELECT
                t.id,
                t.fecha_traslado::timestamp AS FechaTraslado,
                ao.nombre                 AS AulaOrigenNombre,
                ad.nombre                 AS AulaDestinoNombre,
                fa.nombre_completo        AS FuncionarioAnteriorNombre,
                fn.nombre_completo        AS FuncionarioNuevoNombre,
                t.motivo
            FROM traslados t
            LEFT JOIN aulas ao        ON ao.id = t.aula_origen_id
            LEFT JOIN aulas ad        ON ad.id = t.aula_destino_id
            LEFT JOIN funcionarios fa ON fa.id = t.funcionario_anterior_id
            LEFT JOIN funcionarios fn ON fn.id = t.funcionario_nuevo_id
            WHERE t.bien_id = @Id
            ORDER BY t.fecha_traslado DESC, t.id DESC;";

        expediente.Traslados = (await cn.QueryAsync<ExpedienteTrasladoDTO>(
            sqlTraslados, new { Id = bienId })).ToList();

        // ═══════════════════════════════════════════════════════════
        // 5. Depreciación histórica
        // ═══════════════════════════════════════════════════════════
        const string sqlDepreciacion = @"
            SELECT
                id,
                periodo_anio             AS PeriodoAnio,
                periodo_mes              AS PeriodoMes,
                valor_inicial            AS ValorInicial,
                valor_residual           AS ValorResidual,
                depreciacion_mes         AS DepreciacionMes,
                depreciacion_acumulada   AS DepreciacionAcumulada,
                valor_neto               AS ValorNeto
            FROM depreciacion
            WHERE bien_id = @Id
            ORDER BY periodo_anio DESC, periodo_mes DESC;";

        expediente.Depreciaciones = (await cn.QueryAsync<ExpedienteDepreciacionDTO>(
            sqlDepreciacion, new { Id = bienId })).ToList();

        // ═══════════════════════════════════════════════════════════
        // 6. Tomas físicas
        // ═══════════════════════════════════════════════════════════
        const string sqlTomas = @"
            SELECT
                d.id,
                d.toma_fisica_id          AS TomaFisicaId,
                t.codigo                  AS TomaCodigo,
                t.nombre                  AS TomaNombre,
                d.codigo_snapshot         AS CodigoSnapshot,
                d.ubicacion_real          AS UbicacionReal,
                f.nombre_completo         AS FuncionarioRealNombre,
                d.estado_fisico_real      AS EstadoFisicoReal,
                d.tipo,
                d.encontrado,
                d.fecha_escaneo           AS FechaEscaneo,
                d.observaciones
            FROM toma_fisica_detalle d
            INNER JOIN tomas_fisicas t ON t.id = d.toma_fisica_id
            LEFT JOIN funcionarios f   ON f.id = d.funcionario_real_id
            WHERE d.bien_id = @Id
            ORDER BY d.fecha_escaneo DESC NULLS LAST, d.id DESC;";

        expediente.TomasFisicas = (await cn.QueryAsync<ExpedienteTomaFisicaDTO>(
            sqlTomas, new { Id = bienId })).ToList();

        return expediente;
    }
}