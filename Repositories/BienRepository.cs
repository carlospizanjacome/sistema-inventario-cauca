using Almacen.DTOs;
using Almacen.Helpers;
using Almacen.Interfaces;
using Almacen.Models;
using Almacen.Services;
using Dapper;
using Npgsql;

namespace Almacen.Repositories
{
    public class BienRepository : IBienRepository
    {
        // ═══════════════════════════════════════════════════════════
        // Constantes de dominio (cero strings mágicos repetidos)
        // ═══════════════════════════════════════════════════════════
        private const string TIPO_DEVOLUTIVO = "devolutivo";
        private const string TIPO_CONSUMO = "consumo";
        private const string PREFIJO_DEVO = "DEVO";
        private const string PREFIJO_CONS = "CONS";
        private const string SQLSTATE_UNIQUE_VIOLATION = "23505";

        private readonly IConfiguration _configuration;
        private readonly UsuarioSesionService _sesion;

        public BienRepository(IConfiguration configuration, UsuarioSesionService sesion)
        {
            _configuration = configuration;
            _sesion = sesion;
        }

        private NpgsqlConnection Connection
            => new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));

        // ═══════════════════════════════════════════════════════════
        // SELECT base
        // ═══════════════════════════════════════════════════════════
        private const string BaseSelect = @"
            SELECT
                b.id, b.codigo, b.nombre, b.descripcion,
                b.categoria_id AS CategoriaId,
                b.tipo_bien AS TipoBien,
                b.marca, b.modelo, b.serie,
                b.valor_adquisicion AS ValorAdquisicion,
                b.fecha_adquisicion::timestamp AS FechaAdquisicion,
                b.estado_fisico AS EstadoFisico,
                b.ubicacion, b.responsable, b.activo,
                b.created_at AS CreatedAt,
                b.updated_at AS UpdatedAt,
                b.institucion_id AS InstitucionId,
                b.sede_id AS SedeId,
                b.aula_id AS AulaId,
                b.funcionario_id AS FuncionarioId,
                b.vida_util_id AS VidaUtilId,
                b.codigo_qr AS CodigoQr,
                b.valor_residual AS ValorResidual,
                b.cantidad,
                b.depreciacion_acumulada AS DepreciacionAcumulada,
                b.valor_neto AS ValorNeto,
                c.nombre  AS CategoriaNombre,
                c.codigo_cgn AS CodigoCgn,
                vu.vida_util_meses AS VidaUtilMeses,
                a.nombre  AS AulaNombre,
                bl.nombre AS BloqueNombre,
                s.nombre  AS SedeNombre,
                f.nombre_completo AS FuncionarioNombre
            FROM bienes b
            LEFT JOIN categorias c    ON c.id = b.categoria_id
            LEFT JOIN catalogo_cgn vu ON vu.id = b.vida_util_id
            LEFT JOIN aulas a         ON a.id = b.aula_id
            LEFT JOIN bloques bl      ON bl.id = a.bloque_id
            LEFT JOIN sedes s         ON s.id = b.sede_id
            LEFT JOIN funcionarios f  ON f.id = b.funcionario_id";

        // ═══════════════════════════════════════════════════════════
        // Reglas de negocio delegadas a la sesión (no en SQL)
        // ═══════════════════════════════════════════════════════════
        private bool DebeFiltrarPorSede()
            => !_sesion.VeTodasLasSedes;

        // ═══════════════════════════════════════════════════════════
        // Construcción de filtros en C# (nunca en SQL)
        // ═══════════════════════════════════════════════════════════
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

            var sql = condiciones.Count > 0
                ? " AND " + string.Join(" AND ", condiciones)
                : string.Empty;

            return (sql, parametros);
        }

        // ═══════════════════════════════════════════════════════════
        // Asignar sede obligatoria (usado en Crear/Actualizar)
        // ═══════════════════════════════════════════════════════════
        private void AsignarSedeObligatoria(Bien bien)
        {
            if (!_sesion.VeTodasLasSedes)
            {
                // Almacenista/otros → se fuerza su sede
                bien.SedeId = _sesion.SedeId;
            }
            else if (bien.SedeId == null)
            {
                // Rector/Admin que no eligió → error
                throw new InvalidOperationException(
                    "Debe seleccionar una sede para el bien.");
            }

            // Validación final
            if (bien.SedeId == null)
            {
                throw new InvalidOperationException(
                    "No se pudo determinar la sede del bien. " +
                    "Contacte al administrador del sistema.");
            }
        }

        // ═══════════════════════════════════════════════════════════
        // OBTENER TODOS
        // ═══════════════════════════════════════════════════════════
        public async Task<IEnumerable<Bien>> ObtenerTodosAsync()
        {
            var (filtro, parametros) = ConstruirFiltros("b");
            parametros.Add("TipoDevolutivo", TIPO_DEVOLUTIVO);

            var sql = $"{BaseSelect} WHERE b.tipo_bien = @TipoDevolutivo {filtro} ORDER BY b.codigo;";

            using var connection = Connection;
            return await connection.QueryAsync<Bien>(sql, parametros);
        }

        // ═══════════════════════════════════════════════════════════
        // OBTENER POR ID
        // ═══════════════════════════════════════════════════════════
        public async Task<Bien?> ObtenerPorIdAsync(int id)
        {
            var (filtro, parametros) = ConstruirFiltros("b");
            parametros.Add("Id", id);

            var sql = $"{BaseSelect} WHERE b.id = @Id {filtro};";

            using var connection = Connection;
            return await connection.QueryFirstOrDefaultAsync<Bien>(sql, parametros);
        }

        // ═══════════════════════════════════════════════════════════
        // OBTENER POR CÓDIGO
        // ═══════════════════════════════════════════════════════════
        public async Task<Bien?> ObtenerPorCodigoAsync(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo))
                return null;

            var (filtro, parametros) = ConstruirFiltros("b");
            parametros.Add("Codigo", codigo.Trim());

            var sql = $"{BaseSelect} WHERE b.codigo = @Codigo {filtro};";

            using var connection = Connection;
            return await connection.QueryFirstOrDefaultAsync<Bien>(sql, parametros);
        }

        // ═══════════════════════════════════════════════════════════
        // EXISTE CÓDIGO (siempre dentro de la institución)
        // ═══════════════════════════════════════════════════════════
        public async Task<bool> ExisteCodigoAsync(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo))
                return false;

            const string sql = @"
                SELECT EXISTS(
                    SELECT 1 FROM bienes
                    WHERE institucion_id = @InstitucionId
                      AND codigo = @Codigo
                );";

            using var connection = Connection;
            return await connection.ExecuteScalarAsync<bool>(sql,
                new { Codigo = codigo.Trim(), InstitucionId = _sesion.InstitucionId });
        }

        // ═══════════════════════════════════════════════════════════
        // CREAR
        // ═══════════════════════════════════════════════════════════
        public async Task<int> CrearAsync(Bien bien)
        {
            // Institucion
            if (!_sesion.EsSuperAdmin)
                bien.InstitucionId = _sesion.InstitucionId;

            // Sede (obligatoria)
            AsignarSedeObligatoria(bien);

            // Cálculos
            bien.DepreciacionAcumulada = 0;
            bien.ValorNeto = bien.ValorAdquisicion - bien.ValorResidual;

            bool codigoVinoDelUsuario = !string.IsNullOrWhiteSpace(bien.Codigo);

            const string sql = @"
                INSERT INTO bienes
                (codigo, nombre, descripcion, categoria_id, tipo_bien,
                 marca, modelo, serie, valor_adquisicion, fecha_adquisicion,
                 estado_fisico, ubicacion, responsable, activo,
                 institucion_id, sede_id, aula_id, funcionario_id, vida_util_id,
                 codigo_qr, valor_residual, cantidad,
                 depreciacion_acumulada, valor_neto, fecha_ultimo_calculo,
                 created_at, updated_at)
                VALUES
                (@Codigo, @Nombre, @Descripcion, @CategoriaId, @TipoBien,
                 @Marca, @Modelo, @Serie, @ValorAdquisicion, @FechaAdquisicion,
                 @EstadoFisico, @Ubicacion, @Responsable, @Activo,
                 @InstitucionId, @SedeId, @AulaId, @FuncionarioId, @VidaUtilId,
                 @CodigoQr, @ValorResidual, @Cantidad,
                 @DepreciacionAcumulada, @ValorNeto, NOW(),
                 NOW(), NOW())
                RETURNING id;";

            // ── Caso A: sin código → autogenerar ──
            if (!codigoVinoDelUsuario)
            {
                const int maxIntentos = 3;
                for (int intento = 1; intento <= maxIntentos; intento++)
                {
                    try
                    {
                        bien.Codigo = await GenerarSiguienteCodigoAsync(
                            bien.TipoBien ?? TIPO_DEVOLUTIVO);

                        using var connection = Connection;
                        return await connection.ExecuteScalarAsync<int>(sql, bien);
                    }
                    catch (PostgresException ex)
                        when (ex.SqlState == SQLSTATE_UNIQUE_VIOLATION && intento < maxIntentos)
                    {
                        bien.Codigo = string.Empty;
                        await Task.Delay(50);
                    }
                }

                throw new InvalidOperationException(
                    "No se pudo generar un código único después de 3 intentos.");
            }

            // ── Caso B: con código → sin reintentos ──
            try
            {
                using var connection = Connection;
                return await connection.ExecuteScalarAsync<int>(sql, bien);
            }
            catch (PostgresException ex) when (ex.SqlState == SQLSTATE_UNIQUE_VIOLATION)
            {
                throw new InvalidOperationException(
                    $"El código '{bien.Codigo}' ya existe en el sistema. " +
                    $"No se puede crear un bien duplicado con el mismo código contable.");
            }
        }

        // ═══════════════════════════════════════════════════════════
        // Generación de código (sin strings mágicos)
        // ═══════════════════════════════════════════════════════════
        private async Task<string> GenerarSiguienteCodigoAsync(string tipoBien)
        {
            var prefijo = tipoBien == TIPO_CONSUMO ? PREFIJO_CONS : PREFIJO_DEVO;
            var anio = DateTime.Now.Year;
            var patron = $"{prefijo}-{anio}-%";
            var regex = $"^{prefijo}-{anio}-\\d+$";

            const string sql = @"
                SELECT COALESCE(MAX(CAST(SUBSTRING(codigo FROM '[0-9]+$') AS INTEGER)), 0)
                FROM bienes
                WHERE institucion_id = @InstitucionId
                  AND tipo_bien = @TipoBien
                  AND codigo LIKE @Patron
                  AND codigo ~ @Regex;";

            using var connection = Connection;
            var ultimo = await connection.ExecuteScalarAsync<int>(sql, new
            {
                InstitucionId = _sesion.InstitucionId,
                TipoBien = tipoBien,
                Patron = patron,
                Regex = regex
            });

            return $"{prefijo}-{anio}-{(ultimo + 1):D3}";
        }

        // ═══════════════════════════════════════════════════════════
        // ACTUALIZAR
        // ═══════════════════════════════════════════════════════════
        public async Task ActualizarAsync(Bien bien)
        {
            decimal depreciacionActual;
            using (var cnLeer = Connection)
            {
                depreciacionActual = await cnLeer.ExecuteScalarAsync<decimal>(
                    "SELECT COALESCE(depreciacion_acumulada, 0) FROM bienes WHERE id = @Id;",
                    new { Id = bien.Id });
            }

            bien.DepreciacionAcumulada = depreciacionActual;
            bien.ValorNeto = bien.ValorAdquisicion - depreciacionActual - bien.ValorResidual;

            if (!_sesion.EsSuperAdmin)
                bien.InstitucionId = _sesion.InstitucionId;

            AsignarSedeObligatoria(bien);

            const string sql = @"
                UPDATE bienes
                SET codigo = @Codigo, nombre = @Nombre, descripcion = @Descripcion,
                    categoria_id = @CategoriaId, tipo_bien = @TipoBien,
                    marca = @Marca, modelo = @Modelo, serie = @Serie,
                    valor_adquisicion = @ValorAdquisicion, fecha_adquisicion = @FechaAdquisicion,
                    estado_fisico = @EstadoFisico, ubicacion = @Ubicacion,
                    responsable = @Responsable, activo = @Activo,
                    sede_id = @SedeId, aula_id = @AulaId, funcionario_id = @FuncionarioId,
                    vida_util_id = @VidaUtilId, codigo_qr = @CodigoQr,
                    valor_residual = @ValorResidual, cantidad = @Cantidad,
                    valor_neto = @ValorNeto,
                    updated_at = NOW()
                WHERE id = @Id;";

            using var connection = Connection;
            await connection.ExecuteAsync(sql, bien);
        }

        // ═══════════════════════════════════════════════════════════
        // ELIMINAR
        // ═══════════════════════════════════════════════════════════
        public async Task<bool> EliminarAsync(int id)
        {
            using var connection = Connection;
            var filas = await connection.ExecuteAsync(
                "DELETE FROM bienes WHERE id = @Id;", new { Id = id });
            return filas > 0;
        }

        // ═══════════════════════════════════════════════════════════
        // VERIFICAR COMPROMISO
        // ═══════════════════════════════════════════════════════════
        public async Task<CompromisoDTO> VerificarCompromisoAsync(int bienId)
        {
            const string sql = @"
                SELECT 'Entrada'      AS Tipo, COUNT(*)::int AS Cantidad, MAX(numero_factura) AS Referencia
                FROM entradas WHERE bien_id = @BienId
                HAVING COUNT(*) > 0

                UNION ALL

                SELECT 'Salida', COUNT(*)::int, MAX(tipo_baja)
                FROM salidas WHERE bien_id = @BienId
                HAVING COUNT(*) > 0

                UNION ALL

                SELECT 'Traslado', COUNT(*)::int, NULL
                FROM traslados WHERE bien_id = @BienId
                HAVING COUNT(*) > 0

                UNION ALL

                SELECT 'Movimiento de kárdex', COUNT(*)::int, NULL
                FROM movimientos_consumo WHERE bien_id = @BienId
                HAVING COUNT(*) > 0

                UNION ALL

                SELECT 'Toma física', COUNT(*)::int, MAX(codigo_snapshot)
                FROM toma_fisica_detalle
                WHERE bien_id = @BienId AND tipo <> 'PENDIENTE'
                HAVING COUNT(*) > 0

                UNION ALL

                SELECT 'Depreciación', COUNT(*)::int, NULL
                FROM depreciacion WHERE bien_id = @BienId
                HAVING COUNT(*) > 0

                UNION ALL

                SELECT 'Préstamo', COUNT(*)::int, NULL
                FROM prestamos WHERE bien_id = @BienId
                HAVING COUNT(*) > 0

                UNION ALL

                SELECT 'Mantenimiento', COUNT(*)::int, NULL
                FROM mantenimientos WHERE bien_id = @BienId
                HAVING COUNT(*) > 0

                UNION ALL

                SELECT 'Garantía', COUNT(*)::int, MAX(numero_garantia)
                FROM garantias WHERE bien_id = @BienId
                HAVING COUNT(*) > 0;";

            using var connection = Connection;

            var rows = (await connection.QueryAsync<(string Tipo, int Cantidad, string? Referencia)>(
                sql, new { BienId = bienId })).ToList();

            var dto = new CompromisoDTO();

            foreach (var r in rows)
            {
                dto.Detalles.Add(new DetalleCompromiso
                {
                    Tipo = r.Tipo,
                    Cantidad = r.Cantidad,
                    Referencia = r.Referencia,
                    Icono = r.Tipo switch
                    {
                        "Entrada" => "bi-box-arrow-in-down",
                        "Salida" => "bi-box-arrow-up",
                        "Traslado" => "bi-arrow-left-right",
                        "Movimiento de kárdex" => "bi-journal-text",
                        "Toma física" => "bi-clipboard-check",
                        "Depreciación" => "bi-graph-down-arrow",
                        "Préstamo" => "bi-arrow-repeat",
                        "Mantenimiento" => "bi-tools",
                        "Garantía" => "bi-shield-check",
                        _ => "bi-info-circle"
                    }
                });
            }

            return dto;
        }

        // ═══════════════════════════════════════════════════════════
        // PAGINADO simple
        // ═══════════════════════════════════════════════════════════
        public async Task<ResultadoPaginado<Bien>> ObtenerPaginadoAsync(
            int pagina = 1, int tamano = 25, string? filtroTexto = null)
        {
            if (pagina < 1) pagina = 1;
            if (tamano < 1) tamano = 25;
            if (tamano > 200) tamano = 200;

            var (filtroFiltros, parametros) = ConstruirFiltros("b");
            parametros.Add("TipoDevolutivo", TIPO_DEVOLUTIVO);

            var filtroBusqueda = string.Empty;
            if (!string.IsNullOrWhiteSpace(filtroTexto))
            {
                filtroBusqueda = " AND (b.codigo ILIKE @Buscar OR b.nombre ILIKE @Buscar)";
                parametros.Add("Buscar", $"%{filtroTexto}%");
            }

            parametros.Add("Tamano", tamano);
            parametros.Add("Offset", (pagina - 1) * tamano);

            var sqlCount = $@"
                SELECT COUNT(*) FROM bienes b
                WHERE b.tipo_bien = @TipoDevolutivo {filtroFiltros} {filtroBusqueda};";

            var sqlData = $@"
                {BaseSelect}
                WHERE b.tipo_bien = @TipoDevolutivo {filtroFiltros} {filtroBusqueda}
                ORDER BY b.codigo
                LIMIT @Tamano OFFSET @Offset;";

            using var connection = Connection;

            var total = await connection.ExecuteScalarAsync<int>(sqlCount, parametros);
            var items = (await connection.QueryAsync<Bien>(sqlData, parametros)).ToList();

            return new ResultadoPaginado<Bien>
            {
                Items = items,
                TotalRegistros = total,
                PaginaActual = pagina,
                TamanoPagina = tamano
            };
        }

        // ═══════════════════════════════════════════════════════════
        // PAGINADO con filtros (incluye SedeId del filtro)
        // ═══════════════════════════════════════════════════════════
        public async Task<ResultadoPaginado<Bien>> ObtenerPaginadoConFiltrosAsync(
            FiltroBienDTO filtro, int pagina = 1, int tamano = 25)
        {
            if (pagina < 1) pagina = 1;
            if (tamano < 1) tamano = 25;
            if (tamano > 200) tamano = 200;

            var (filtroBase, parametros) = ConstruirFiltros("b");
            parametros.Add("TipoDevolutivo", TIPO_DEVOLUTIVO);

            var condiciones = new List<string>();

            if (!string.IsNullOrWhiteSpace(filtro.Texto))
            {
                condiciones.Add("(b.codigo ILIKE @Texto OR b.nombre ILIKE @Texto OR b.serie ILIKE @Texto)");
                parametros.Add("Texto", $"%{filtro.Texto}%");
            }
            if (filtro.CategoriaId.HasValue)
            {
                condiciones.Add("b.categoria_id = @CategoriaId");
                parametros.Add("CategoriaId", filtro.CategoriaId.Value);
            }
            if (filtro.SedeId.HasValue)
            {
                condiciones.Add("b.sede_id = @FiltroSedeId");
                parametros.Add("FiltroSedeId", filtro.SedeId.Value);
            }
            if (filtro.AulaId.HasValue)
            {
                condiciones.Add("b.aula_id = @AulaId");
                parametros.Add("AulaId", filtro.AulaId.Value);
            }
            if (filtro.FuncionarioId.HasValue)
            {
                condiciones.Add("b.funcionario_id = @FuncionarioId");
                parametros.Add("FuncionarioId", filtro.FuncionarioId.Value);
            }
            if (!string.IsNullOrWhiteSpace(filtro.EstadoFisico))
            {
                condiciones.Add("b.estado_fisico = @EstadoFisico");
                parametros.Add("EstadoFisico", filtro.EstadoFisico);
            }
            if (filtro.Activo.HasValue)
            {
                condiciones.Add("b.activo = @Activo");
                parametros.Add("Activo", filtro.Activo.Value);
            }
            if (filtro.FechaDesde.HasValue)
            {
                condiciones.Add("b.fecha_adquisicion >= @FechaDesde");
                parametros.Add("FechaDesde", filtro.FechaDesde.Value);
            }
            if (filtro.FechaHasta.HasValue)
            {
                condiciones.Add("b.fecha_adquisicion <= @FechaHasta");
                parametros.Add("FechaHasta", filtro.FechaHasta.Value);
            }

            var whereExtra = condiciones.Count > 0
                ? " AND " + string.Join(" AND ", condiciones)
                : string.Empty;

            parametros.Add("Tamano", tamano);
            parametros.Add("Offset", (pagina - 1) * tamano);

            var sqlCount = $@"
                SELECT COUNT(*) FROM bienes b
                WHERE b.tipo_bien = @TipoDevolutivo {filtroBase} {whereExtra};";

            var sqlData = $@"
                {BaseSelect}
                WHERE b.tipo_bien = @TipoDevolutivo {filtroBase} {whereExtra}
                ORDER BY b.codigo
                LIMIT @Tamano OFFSET @Offset;";

            using var connection = Connection;

            var total = await connection.ExecuteScalarAsync<int>(sqlCount, parametros);
            var items = (await connection.QueryAsync<Bien>(sqlData, parametros)).ToList();

            return new ResultadoPaginado<Bien>
            {
                Items = items,
                TotalRegistros = total,
                PaginaActual = pagina,
                TamanoPagina = tamano
            };
        }

        // ═══════════════════════════════════════════════════════════
        // POR FUNCIONARIO
        // ═══════════════════════════════════════════════════════════
        public async Task<IEnumerable<Bien>> ObtenerPorFuncionarioAsync(int funcionarioId)
        {
            var (filtro, parametros) = ConstruirFiltros("b");
            parametros.Add("FuncionarioId", funcionarioId);
            parametros.Add("TipoDevolutivo", TIPO_DEVOLUTIVO);

            var sql = $@"
                {BaseSelect}
                WHERE b.funcionario_id = @FuncionarioId
                  AND b.activo = TRUE
                  AND b.tipo_bien = @TipoDevolutivo
                  {filtro}
                ORDER BY b.codigo;";

            using var connection = Connection;
            return await connection.QueryAsync<Bien>(sql, parametros);
        }
    }
}