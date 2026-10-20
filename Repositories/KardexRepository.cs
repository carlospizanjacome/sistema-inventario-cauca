using Almacen.DTOs;
using Almacen.Helpers;
using Almacen.Interfaces;
using Almacen.Services;
using Dapper;
using Npgsql;
using System.Data;

namespace Almacen.Repositories;

public class KardexRepository : IKardexRepository
{
    private readonly string _cs;
    private readonly UsuarioSesionService _sesion;

    public KardexRepository(IConfiguration cfg, UsuarioSesionService sesion)
    {
        _cs = cfg.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta DefaultConnection.");
        _sesion = sesion;
    }

    private bool DebeFiltrarPorSede() => !_sesion.VeTodasLasSedes;

    private (string Sql, DynamicParameters Parametros) ConstruirFiltros(string alias)
    {
        var condiciones = new List<string>();
        var parametros = new DynamicParameters();

        if (!_sesion.EsSuperAdmin && _sesion.InstitucionId > 0)
        {
            condiciones.Add($"{alias}.institucion_id = @InstitucionId");
            parametros.Add("InstitucionId", _sesion.InstitucionId);
        }

        if (DebeFiltrarPorSede())
        {
            condiciones.Add($"{alias}.sede_id = @SedeId");
            parametros.Add("SedeId", _sesion.SedeId!.Value);
        }

        var sql = condiciones.Count > 0 ? " AND " + string.Join(" AND ", condiciones) : string.Empty;
        return (sql, parametros);
    }

    public async Task<int> CrearBienConsumoAsync(BienConsumoDTO dto)
    {
        if (!_sesion.EsSuperAdmin) dto.InstitucionId = _sesion.InstitucionId;
        if (!_sesion.VeTodasLasSedes) dto.SedeId = _sesion.SedeId;

        if (dto.SedeId == null) throw new InvalidOperationException("La sede es obligatoria.");

        const string sql = @"
            INSERT INTO bienes
                (codigo, nombre, categoria_id, tipo_bien, unidad_medida, stock_actual,
                 stock_minimo, cpp_actual, valor_stock, institucion_id, sede_id, activo, 
                 created_at, updated_at)
            VALUES
                (@Codigo, @Nombre, @CategoriaId, 'consumo', @UnidadMedida, @StockActual,
                 @StockMinimo, @CppActual, (@StockActual * @CppActual), @InstitucionId, @SedeId, TRUE,
                 NOW(), NOW())
            RETURNING id;";

        using var cn = new NpgsqlConnection(_cs);

        // ── NUEVO: Generar código automático si viene vacío ──
        if (string.IsNullOrWhiteSpace(dto.Codigo))
        {
            const int maxIntentos = 3;
            for (int intento = 1; intento <= maxIntentos; intento++)
            {
                try
                {
                    dto.Codigo = await GenerarSiguienteCodigoConsumoAsync();
                    return await cn.ExecuteScalarAsync<int>(sql, dto);
                }
                catch (PostgresException ex) when (ex.SqlState == "23505" && intento < maxIntentos)
                {
                    dto.Codigo = string.Empty;
                    await Task.Delay(50);
                }
            }
            throw new InvalidOperationException("No se pudo generar un código único después de 3 intentos.");
        }

        // Si viene código manual
        try
        {
            return await cn.ExecuteScalarAsync<int>(sql, dto);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            throw new InvalidOperationException($"El código '{dto.Codigo}' ya existe en el sistema.");
        }
    }

    // ── NUEVO: Función para calcular el código perfecto ──
    private async Task<string> GenerarSiguienteCodigoConsumoAsync()
    {
        var prefijo = "CONS";
        var anio = DateTime.Now.Year;
        var patron = $"{prefijo}-{anio}-%";
        var regex = $"^{prefijo}-{anio}-\\d+$";

        const string sql = @"
            SELECT COALESCE(MAX(CAST(SUBSTRING(codigo FROM '[0-9]+$') AS INTEGER)), 0)
            FROM bienes
            WHERE institucion_id = @InstitucionId
              AND tipo_bien = 'consumo'
              AND codigo LIKE @Patron
              AND codigo ~ @Regex;";

        using var cn = new NpgsqlConnection(_cs);
        var ultimo = await cn.ExecuteScalarAsync<int>(sql, new
        {
            InstitucionId = _sesion.InstitucionId,
            Patron = patron,
            Regex = regex
        });

        return $"{prefijo}-{anio}-{(ultimo + 1):D3}";
    }

    public async Task<bool> ActualizarBienConsumoAsync(BienConsumoDTO dto)
    {
        const string sql = @"
            UPDATE bienes
            SET nombre = @Nombre,
                categoria_id = @CategoriaId,
                unidad_medida = @UnidadMedida,
                stock_minimo = @StockMinimo,
                updated_at = NOW()
            WHERE id = @Id AND tipo_bien = 'consumo' AND activo = TRUE;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteAsync(sql, dto) > 0;
    }

    public async Task<bool> EliminarBienConsumoAsync(int id)
    {
        const string sql = "UPDATE bienes SET activo = FALSE, updated_at = NOW() WHERE id = @Id AND tipo_bien = 'consumo';";
        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteAsync(sql, new { Id = id }) > 0;
    }

    public async Task<bool> ExisteCodigoConsumoAsync(string codigo, int? excluirId = null)
    {
        var (filtro, parametros) = ConstruirFiltros("b");
        parametros.Add("Codigo", codigo);
        parametros.Add("ExcluirId", excluirId, DbType.Int32);

        var sql = $@"
            SELECT COUNT(*) FROM bienes b
            WHERE b.codigo = @Codigo AND b.activo = TRUE
              {filtro}
              AND (@ExcluirId IS NULL OR b.id <> @ExcluirId);";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteScalarAsync<int>(sql, parametros) > 0;
    }

    public async Task<ResultadoPaginado<BienConsumoDTO>> ObtenerBienesConsumoPaginadoAsync(
        int pagina, int tamano, string? filtroTexto, int? categoriaId, bool? bajoStock)
    {
        if (pagina < 1) pagina = 1;
        if (tamano < 1) tamano = 25;

        var (filtroSql, parametros) = ConstruirFiltros("b");
        var condiciones = new List<string> { "b.tipo_bien = 'consumo'", "b.activo = TRUE" };

        if (!string.IsNullOrWhiteSpace(filtroTexto))
        {
            condiciones.Add("(b.codigo ILIKE @Buscar OR b.nombre ILIKE @Buscar)");
            parametros.Add("Buscar", $"%{filtroTexto}%");
        }
        if (categoriaId.HasValue && categoriaId > 0)
        {
            condiciones.Add("b.categoria_id = @CatId");
            parametros.Add("CatId", categoriaId.Value);
        }
        if (bajoStock == true) condiciones.Add("b.stock_actual <= b.stock_minimo");

        var whereExtra = " AND " + string.Join(" AND ", condiciones);
        parametros.Add("Tamano", tamano);
        parametros.Add("Offset", (pagina - 1) * tamano);

        var sqlCount = $"SELECT COUNT(*) FROM bienes b WHERE 1=1 {filtroSql} {whereExtra};";
        var sqlData = $@"
            SELECT b.id AS Id, b.codigo AS Codigo, b.nombre AS Nombre, b.categoria_id AS CategoriaId,
                   b.unidad_medida AS UnidadMedida, b.stock_actual AS StockActual, b.stock_minimo AS StockMinimo,
                   b.cpp_actual AS CppActual, b.valor_stock AS ValorStock,
                   (b.stock_actual <= b.stock_minimo) AS BajoStockMinimo
            FROM bienes b
            WHERE 1=1 {filtroSql} {whereExtra}
            ORDER BY b.nombre LIMIT @Tamano OFFSET @Offset;";

        using var cn = new NpgsqlConnection(_cs);
        var total = await cn.ExecuteScalarAsync<int>(sqlCount, parametros);
        var items = (await cn.QueryAsync<BienConsumoDTO>(sqlData, parametros)).ToList();

        return new ResultadoPaginado<BienConsumoDTO>
        {
            Items = items,
            TotalRegistros = total,
            PaginaActual = pagina,
            TamanoPagina = tamano
        };
    }

    public async Task<IEnumerable<BienConsumoDTO>> ObtenerBienesConsumoAsync()
    {
        var (filtroSql, parametros) = ConstruirFiltros("b");
        var sql = $@"
            SELECT b.id AS Id, b.codigo AS Codigo, b.nombre AS Nombre, b.unidad_medida AS UnidadMedida,
                   b.stock_actual AS StockActual, b.stock_minimo AS StockMinimo, b.cpp_actual AS CppActual,
                   b.valor_stock AS ValorStock, (b.stock_actual <= b.stock_minimo) AS BajoStockMinimo
            FROM bienes b
            WHERE b.tipo_bien = 'consumo' AND b.activo = TRUE {filtroSql}
            ORDER BY b.nombre;";
        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<BienConsumoDTO>(sql, parametros);
    }

    public async Task<BienConsumoDTO?> ObtenerBienConsumoPorIdAsync(int id)
    {
        var (filtroSql, parametros) = ConstruirFiltros("b");
        parametros.Add("Id", id);
        var sql = $@"
            SELECT b.id AS Id, b.codigo AS Codigo, b.nombre AS Nombre, b.unidad_medida AS UnidadMedida,
                   b.stock_actual AS StockActual, b.stock_minimo AS StockMinimo, b.cpp_actual AS CppActual,
                   b.valor_stock AS ValorStock, (b.stock_actual <= b.stock_minimo) AS BajoStockMinimo,
                   b.institucion_id AS InstitucionId
            FROM bienes b
            WHERE b.id = @Id AND b.tipo_bien = 'consumo' {filtroSql};";
        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstOrDefaultAsync<BienConsumoDTO>(sql, parametros);
    }

    public async Task<IEnumerable<BienConsumoDTO>> ObtenerBajoStockMinimoAsync()
    {
        var (filtroSql, parametros) = ConstruirFiltros("b");
        var sql = $@"
            SELECT b.id AS Id, b.codigo AS Codigo, b.nombre AS Nombre, b.unidad_medida AS UnidadMedida,
                   b.stock_actual AS StockActual, b.stock_minimo AS StockMinimo, b.cpp_actual AS CppActual,
                   b.valor_stock AS ValorStock, (b.stock_actual <= b.stock_minimo) AS BajoStockMinimo
            FROM bienes b
            WHERE b.tipo_bien = 'consumo' AND b.activo = TRUE AND b.stock_actual <= b.stock_minimo {filtroSql}
            ORDER BY b.stock_actual;";
        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<BienConsumoDTO>(sql, parametros);
    }

    public async Task<IEnumerable<MovimientoConsumoDTO>> ObtenerMovimientosAsync(int? bienId = null)
    {
        var (filtroSql, parametros) = ConstruirFiltros("m");

        var condBien = bienId.HasValue ? " AND m.bien_id = @BienId" : "";
        if (bienId.HasValue) parametros.Add("BienId", bienId.Value);

        var sql = $@"
            SELECT m.id AS Id, m.bien_id AS BienId, m.tipo_movimiento AS TipoMovimiento,
                   m.fecha_movimiento::timestamp AS FechaMovimiento, m.cantidad AS Cantidad,
                   m.costo_unitario AS CostoUnitario, m.valor_movimiento AS ValorMovimiento,
                   m.saldo_cantidad AS SaldoCantidad, m.saldo_valor AS SaldoValor, m.cpp AS Cpp,
                   m.documento_referencia AS DocumentoReferencia, m.observaciones AS Observaciones,
                   m.funcionario_recibe_id AS FuncionarioRecibeId, f.nombre_completo AS FuncionarioRecibeNombre
            FROM movimientos_consumo m
            LEFT JOIN funcionarios f ON f.id = m.funcionario_recibe_id
            WHERE 1=1 {filtroSql} {condBien}
            ORDER BY m.fecha_movimiento DESC, m.id DESC;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<MovimientoConsumoDTO>(sql, parametros);
    }

    public async Task<IEnumerable<MovimientoConsumoDTO>> ObtenerKardexAsync(int bienId)
    {
        return await ObtenerMovimientosAsync(bienId);
    }

    public async Task<CompromisoDTO> VerificarCompromisoConsumoAsync(int bienId)
    {
        return await Task.FromResult(new CompromisoDTO());
    }

    public async Task<int> RegistrarMovimientoAsync(MovimientoConsumoDTO dto)
    {
        var bien = await ObtenerBienConsumoPorIdAsync(dto.BienId);
        if (bien is null) throw new InvalidOperationException("El bien no existe o no pertenece a su sede.");

        using var cn = new NpgsqlConnection(_cs);
        await cn.OpenAsync();
        using var tx = await cn.BeginTransactionAsync();

        try
        {
            int sedeId = _sesion.VeTodasLasSedes ? (dto.SedeId ?? _sesion.SedeId ?? 0) : _sesion.SedeId.Value;
            if (sedeId == 0) throw new InvalidOperationException("No se pudo determinar la sede.");

            var ultimoSaldo = await cn.QueryFirstOrDefaultAsync<dynamic>(
                @"SELECT saldo_cantidad, saldo_valor, cpp FROM movimientos_consumo 
                  WHERE bien_id = @BienId ORDER BY fecha_movimiento DESC, id DESC LIMIT 1;",
                new { BienId = dto.BienId }, tx);

            decimal saldoCantAnterior = (decimal)(ultimoSaldo?.saldo_cantidad ?? bien.StockActual);
            decimal saldoValAnterior = (decimal)(ultimoSaldo?.saldo_valor ?? bien.ValorStock);
            decimal cppAnterior = (decimal)(ultimoSaldo?.cpp ?? bien.CppActual);

            decimal nuevoCosto = (dto.TipoMovimiento == "ENTRADA" || dto.TipoMovimiento == "AJUSTE_POS") ? dto.CostoUnitario : cppAnterior;

            decimal saldoCantNuevo = 0, saldoValNuevo = 0, cppNuevo = 0;

            if (dto.TipoMovimiento == "ENTRADA" || dto.TipoMovimiento == "AJUSTE_POS")
            {
                saldoCantNuevo = saldoCantAnterior + dto.Cantidad;
                saldoValNuevo = saldoValAnterior + (dto.Cantidad * nuevoCosto);
                cppNuevo = saldoCantNuevo > 0 ? (saldoValNuevo / saldoCantNuevo) : 0;
            }
            else
            {
                saldoCantNuevo = saldoCantAnterior - dto.Cantidad;
                saldoValNuevo = saldoCantNuevo * cppAnterior;
                cppNuevo = cppAnterior;
            }

            decimal valorMovimiento = dto.Cantidad * nuevoCosto;

            const string sqlInsert = @"
                INSERT INTO movimientos_consumo
                    (bien_id, institucion_id, sede_id, tipo_movimiento, fecha_movimiento, cantidad, costo_unitario,
                     valor_movimiento, saldo_cantidad, saldo_valor, cpp, documento_referencia,
                     observaciones, funcionario_recibe_id)
                VALUES
                    (@BienId, @InstitucionId, @SedeId, @TipoMovimiento, @FechaMovimiento, @Cantidad, @CostoUnitario,
                     @ValorMovimiento, @SaldoCantidad, @SaldoValor, @Cpp, @DocumentoReferencia,
                     @Observaciones, @FuncionarioRecibeId)
                RETURNING id;";

            var idMov = await cn.ExecuteScalarAsync<int>(sqlInsert, new
            {
                dto.BienId,
                InstitucionId = bien.InstitucionId,
                SedeId = sedeId,
                dto.TipoMovimiento,
                FechaMovimiento = dto.FechaMovimiento ?? DateTime.Today,
                dto.Cantidad,
                CostoUnitario = nuevoCosto,
                ValorMovimiento = valorMovimiento,
                SaldoCantidad = saldoCantNuevo,
                SaldoValor = saldoValNuevo,
                Cpp = cppNuevo,
                dto.DocumentoReferencia,
                dto.Observaciones,
                dto.FuncionarioRecibeId
            }, tx);

            await cn.ExecuteAsync(@"
                UPDATE bienes 
                SET stock_actual = @SaldoCantidad,
                    cpp_actual = @Cpp,
                    valor_stock = @SaldoValor,
                    updated_at = NOW()
                WHERE id = @BienId;",
                new { SaldoCantidad = saldoCantNuevo, Cpp = cppNuevo, SaldoValor = saldoValNuevo, BienId = dto.BienId }, tx);

            await tx.CommitAsync();
            return idMov;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}