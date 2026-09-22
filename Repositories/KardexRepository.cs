using Almacen.DTOs;
using Almacen.Helpers;
using Almacen.Interfaces;
using Almacen.Services;
using Dapper;
using Npgsql;

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

    // ═══════════════════════════════════════════════════════
    // BIENES DE CONSUMO
    // ═══════════════════════════════════════════════════════

    private const string BaseBienConsumo = @"
        SELECT b.id, b.institucion_id AS InstitucionId, b.codigo, b.nombre,
               b.descripcion, b.categoria_id AS CategoriaId,
               b.unidad_medida AS UnidadMedida,
               b.stock_minimo AS StockMinimo,
               b.stock_actual AS StockActual,
               b.cpp_actual AS CppActual,
               b.valor_stock AS ValorStock,
               b.activo,
               c.nombre AS CategoriaNombre
        FROM bienes b
        LEFT JOIN categorias c ON c.id = b.categoria_id
        WHERE b.tipo_bien = 'consumo'";

    public async Task<IEnumerable<BienConsumoDTO>> ObtenerBienesConsumoAsync()
    {
        var (filtro, param) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "b");

        var sql = $"{BaseBienConsumo} {filtro} ORDER BY b.nombre;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<BienConsumoDTO>(sql, param);
    }

    public async Task<BienConsumoDTO?> ObtenerBienConsumoPorIdAsync(int id)
    {
        var (filtro, _) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "b");

        var sql = $"{BaseBienConsumo} AND b.id = @Id {filtro};";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryFirstOrDefaultAsync<BienConsumoDTO>(sql,
            new { Id = id, InstitucionId = _sesion.InstitucionId });
    }

    public async Task<int> CrearBienConsumoAsync(BienConsumoDTO dto)
    {
        dto.InstitucionId = _sesion.InstitucionId;

        const string sql = @"
            INSERT INTO bienes
                (institucion_id, codigo, nombre, descripcion, categoria_id,
                 tipo_bien, unidad_medida, stock_minimo, stock_actual,
                 cpp_actual, valor_stock, activo, cantidad, created_at, updated_at)
            VALUES
                (@InstitucionId, @Codigo, @Nombre, @Descripcion, @CategoriaId,
                 'consumo', @UnidadMedida, @StockMinimo, 0,
                 0, 0, @Activo, 1, NOW(), NOW())
            RETURNING id;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteScalarAsync<int>(sql, dto);
    }

    public async Task<bool> ActualizarBienConsumoAsync(BienConsumoDTO dto)
    {
        const string sql = @"
            UPDATE bienes
            SET codigo = @Codigo,
                nombre = @Nombre,
                descripcion = @Descripcion,
                categoria_id = @CategoriaId,
                unidad_medida = @UnidadMedida,
                stock_minimo = @StockMinimo,
                activo = @Activo,
                updated_at = NOW()
            WHERE id = @Id AND tipo_bien = 'consumo';";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteAsync(sql, dto) > 0;
    }

    public async Task<bool> EliminarBienConsumoAsync(int id)
    {
        const string sql = @"
            DELETE FROM bienes
            WHERE id = @Id AND tipo_bien = 'consumo';";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.ExecuteAsync(sql, new { Id = id }) > 0;
    }

    public async Task<bool> ExisteCodigoConsumoAsync(string codigo, int? excluirId = null)
    {
        var (filtro, _) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "b");

        var sql = $@"
            SELECT COUNT(*) FROM bienes b
            WHERE b.tipo_bien = 'consumo'
              AND b.codigo = @Codigo
              AND (@ExcluirId IS NULL OR b.id <> @ExcluirId)
              {filtro};";

        using var cn = new NpgsqlConnection(_cs);
        var n = await cn.ExecuteScalarAsync<int>(sql,
            new { Codigo = codigo, ExcluirId = excluirId, InstitucionId = _sesion.InstitucionId });
        return n > 0;
    }

    // ═══════════════════════════════════════════════════════
    // KARDEX — MOVIMIENTOS
    // ═══════════════════════════════════════════════════════

    private const string BaseMovimiento = @"
        SELECT m.id, m.bien_id AS BienId, m.institucion_id AS InstitucionId,
               m.tipo_movimiento AS TipoMovimiento,
               m.fecha_movimiento::timestamp AS FechaMovimiento,
               m.cantidad, m.costo_unitario AS CostoUnitario,
               m.valor_movimiento AS ValorMovimiento,
               m.saldo_cantidad AS SaldoCantidad,
               m.saldo_valor AS SaldoValor,
               m.cpp, m.documento_referencia AS DocumentoReferencia,
               m.proveedor_id AS ProveedorId,
               m.funcionario_recibe_id AS FuncionarioRecibeId,
               m.observaciones,
               b.codigo AS BienCodigo,
               b.nombre AS BienNombre,
               b.unidad_medida AS UnidadMedida,
               p.nombre AS ProveedorNombre,
               f.nombre_completo AS FuncionarioNombre
        FROM movimientos_consumo m
        LEFT JOIN bienes b       ON b.id = m.bien_id
        LEFT JOIN proveedores p  ON p.id = m.proveedor_id
        LEFT JOIN funcionarios f ON f.id = m.funcionario_recibe_id";

    public async Task<IEnumerable<MovimientoConsumoDTO>> ObtenerMovimientosAsync(int? bienId = null)
    {
        var (filtro, _) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "m");

        var sql = $"{BaseMovimiento} WHERE 1=1 {filtro}";

        if (bienId.HasValue)
            sql += " AND m.bien_id = @BienId";

        sql += " ORDER BY m.fecha_movimiento DESC, m.id DESC LIMIT 500;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<MovimientoConsumoDTO>(sql,
            new { BienId = bienId, InstitucionId = _sesion.InstitucionId });
    }

    public async Task<IEnumerable<MovimientoConsumoDTO>> ObtenerKardexAsync(int bienId)
    {
        var (filtro, _) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "m");

        var sql = $@"
            {BaseMovimiento}
            WHERE m.bien_id = @BienId {filtro}
            ORDER BY m.fecha_movimiento ASC, m.id ASC;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<MovimientoConsumoDTO>(sql,
            new { BienId = bienId, InstitucionId = _sesion.InstitucionId });
    }

    /// <summary>
    /// Registra un movimiento en el kardex y actualiza el bien
    /// (stock_actual, cpp_actual, valor_stock) — TODO EN UNA TRANSACCIÓN.
    /// </summary>
    public async Task<int> RegistrarMovimientoAsync(MovimientoConsumoDTO dto)
    {
        using var cn = new NpgsqlConnection(_cs);
        await cn.OpenAsync();
        using var tx = await cn.BeginTransactionAsync();

        try
        {
            // 1. Obtener estado actual del bien (bloqueando para evitar race conditions)
            const string sqlGet = @"
            SELECT stock_actual AS StockActual,
                   cpp_actual AS CppActual,
                   valor_stock AS ValorStock
            FROM bienes
            WHERE id = @BienId AND tipo_bien = 'consumo'
            FOR UPDATE;";

            var estado = await cn.QueryFirstOrDefaultAsync<EstadoBienConsumo>(
                sqlGet, new { dto.BienId }, tx);

            if (estado is null)
                throw new InvalidOperationException("El bien no existe o no es de consumo.");

            var saldoCant = estado.StockActual;
            var saldoValor = estado.ValorStock;
            var cpp = estado.CppActual;

            // 2. Calcular según tipo de movimiento
            decimal nuevoSaldoCant;
            decimal nuevoSaldoValor;
            decimal nuevoCpp;
            decimal valorMovimiento;
            decimal costoUnitarioUsado;

            switch (dto.TipoMovimiento)
            {
                case "ENTRADA":
                case "AJUSTE_POS":
                    if (dto.CostoUnitario <= 0)
                        throw new InvalidOperationException("El costo unitario debe ser mayor a 0.");

                    valorMovimiento = dto.Cantidad * dto.CostoUnitario;
                    nuevoSaldoCant = saldoCant + dto.Cantidad;
                    nuevoSaldoValor = saldoValor + valorMovimiento;

                    nuevoCpp = nuevoSaldoCant > 0
                        ? Math.Round(nuevoSaldoValor / nuevoSaldoCant, 4)
                        : 0;

                    costoUnitarioUsado = dto.CostoUnitario;
                    break;

                case "SALIDA":
                case "AJUSTE_NEG":
                    if (dto.Cantidad > saldoCant)
                        throw new InvalidOperationException(
                            $"No hay suficiente stock. Disponible: {saldoCant:N2}.");

                    costoUnitarioUsado = cpp;
                    valorMovimiento = Math.Round(dto.Cantidad * cpp, 2);
                    nuevoSaldoCant = saldoCant - dto.Cantidad;
                    nuevoSaldoValor = Math.Round(saldoValor - valorMovimiento, 2);
                    nuevoCpp = cpp;
                    break;

                default:
                    throw new InvalidOperationException("Tipo de movimiento no válido.");
            }

            // 3. Insertar el movimiento
            const string sqlInsert = @"
            INSERT INTO movimientos_consumo
                (bien_id, institucion_id, tipo_movimiento, fecha_movimiento,
                 cantidad, costo_unitario, valor_movimiento,
                 saldo_cantidad, saldo_valor, cpp,
                 documento_referencia, proveedor_id, funcionario_recibe_id,
                 observaciones)
            VALUES
                (@BienId, @InstitucionId, @TipoMovimiento, @FechaMovimiento,
                 @Cantidad, @CostoUnitario, @ValorMovimiento,
                 @SaldoCantidad, @SaldoValor, @Cpp,
                 @DocumentoReferencia, @ProveedorId, @FuncionarioRecibeId,
                 @Observaciones)
            RETURNING id;";

            var idMovimiento = await cn.ExecuteScalarAsync<int>(sqlInsert, new
            {
                dto.BienId,
                InstitucionId = _sesion.InstitucionId,
                dto.TipoMovimiento,
                dto.FechaMovimiento,
                dto.Cantidad,
                CostoUnitario = costoUnitarioUsado,
                ValorMovimiento = valorMovimiento,
                SaldoCantidad = nuevoSaldoCant,
                SaldoValor = nuevoSaldoValor,
                Cpp = nuevoCpp,
                dto.DocumentoReferencia,
                dto.ProveedorId,
                dto.FuncionarioRecibeId,
                dto.Observaciones
            }, tx);

            // 4. Actualizar el bien
            const string sqlUpdate = @"
            UPDATE bienes
            SET stock_actual = @StockActual,
                cpp_actual   = @CppActual,
                valor_stock  = @ValorStock,
                updated_at   = NOW()
            WHERE id = @BienId;";

            await cn.ExecuteAsync(sqlUpdate, new
            {
                BienId = dto.BienId,
                StockActual = nuevoSaldoCant,
                CppActual = nuevoCpp,
                ValorStock = nuevoSaldoValor
            }, tx);

            await tx.CommitAsync();
            return idMovimiento;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Clase interna para mapear el estado del bien en la transacción.
    /// (no puede ser tupla porque un bien sin stock tiene valores 0,
    /// que son indistinguibles del default)
    /// </summary>
    private class EstadoBienConsumo
    {
        public decimal StockActual { get; set; }
        public decimal CppActual { get; set; }
        public decimal ValorStock { get; set; }
    }

    public async Task<IEnumerable<BienConsumoDTO>> ObtenerBajoStockMinimoAsync()
    {
        var (filtro, param) = FiltroInstitucion.Construir(
            _sesion.EsSuperAdmin, _sesion.InstitucionId, tabla: "b");

        var sql = $@"
            {BaseBienConsumo}
            AND b.activo = TRUE
            AND b.stock_actual <= b.stock_minimo
            {filtro}
            ORDER BY b.nombre;";

        using var cn = new NpgsqlConnection(_cs);
        return await cn.QueryAsync<BienConsumoDTO>(sql, param);
    }
}