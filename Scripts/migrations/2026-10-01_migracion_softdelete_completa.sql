-- ═══════════════════════════════════════════════════════════════════
-- MIGRACIÓN COMPLETA: Soft-Delete (Anular en vez de Eliminar)
-- Fecha: 2026-10-01
-- 
-- Objetivo: migrar el sistema para permitir "Anular" (soft-delete)
-- en lugar de "Eliminar" (hard-delete) los registros históricos.
-- 
-- Cumple con:
--   - Ley 87/1993 (Control Interno)
--   - Resolución 533/2015 (CGN)
--   - Guía Control Interno Contable v5.0 (CGN)
--   - Decreto 1075/2015 (FSE)
--   - Requisitos de Contraloría
-- 
-- Aplicar en Neon Console → SQL Editor → Run
-- ═══════════════════════════════════════════════════════════════════

-- ═══════════════════════════════════════════════════════════════════
-- PASO 1: BACKUP (crear branch en Neon ANTES de ejecutar)
-- 
-- En Neon Console → Branches → Create branch
-- Nombre: backup-2026-10-01-pre-softdelete
-- ═══════════════════════════════════════════════════════════════════

-- ═══════════════════════════════════════════════════════════════════
-- PASO 2: AGREGAR COLUMNAS DE ANULACIÓN
-- ═══════════════════════════════════════════════════════════════════

BEGIN;

-- ─── MANTENIMIENTOS ───
ALTER TABLE mantenimientos 
    ADD COLUMN IF NOT EXISTS anulada BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS anulada_por INTEGER,
    ADD COLUMN IF NOT EXISTS anulada_fecha TIMESTAMP,
    ADD COLUMN IF NOT EXISTS anulada_motivo TEXT;

CREATE INDEX IF NOT EXISTS idx_mantenimientos_no_anulados 
    ON mantenimientos(bien_id) WHERE anulada = FALSE;

-- ─── ENTRADAS ───
ALTER TABLE entradas 
    ADD COLUMN IF NOT EXISTS anulada BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS anulada_por INTEGER,
    ADD COLUMN IF NOT EXISTS anulada_fecha TIMESTAMP,
    ADD COLUMN IF NOT EXISTS anulada_motivo TEXT;

CREATE INDEX IF NOT EXISTS idx_entradas_no_anuladas 
    ON entradas(bien_id) WHERE anulada = FALSE;

-- ─── SALIDAS ───
ALTER TABLE salidas 
    ADD COLUMN IF NOT EXISTS anulada BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS anulada_por INTEGER,
    ADD COLUMN IF NOT EXISTS anulada_fecha TIMESTAMP,
    ADD COLUMN IF NOT EXISTS anulada_motivo TEXT;

CREATE INDEX IF NOT EXISTS idx_salidas_no_anuladas 
    ON salidas(bien_id) WHERE anulada = FALSE;

-- ─── TRASLADOS ───
ALTER TABLE traslados 
    ADD COLUMN IF NOT EXISTS anulada BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS anulada_por INTEGER,
    ADD COLUMN IF NOT EXISTS anulada_fecha TIMESTAMP,
    ADD COLUMN IF NOT EXISTS anulada_motivo TEXT;

CREATE INDEX IF NOT EXISTS idx_traslados_no_anulados 
    ON traslados(bien_id) WHERE anulada = FALSE;

-- ─── PRESTAMOS ───
ALTER TABLE prestamos 
    ADD COLUMN IF NOT EXISTS anulada BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS anulada_por INTEGER,
    ADD COLUMN IF NOT EXISTS anulada_fecha TIMESTAMP,
    ADD COLUMN IF NOT EXISTS anulada_motivo TEXT;

CREATE INDEX IF NOT EXISTS idx_prestamos_no_anulados 
    ON prestamos(bien_id) WHERE anulada = FALSE;

-- ─── GARANTIAS ───
ALTER TABLE garantias 
    ADD COLUMN IF NOT EXISTS anulada BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS anulada_por INTEGER,
    ADD COLUMN IF NOT EXISTS anulada_fecha TIMESTAMP,
    ADD COLUMN IF NOT EXISTS anulada_motivo TEXT;

CREATE INDEX IF NOT EXISTS idx_garantias_no_anuladas 
    ON garantias(bien_id) WHERE anulada = FALSE;

-- ─── MOVIMIENTOS_CONSUMO ───
ALTER TABLE movimientos_consumo 
    ADD COLUMN IF NOT EXISTS anulada BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS anulada_por INTEGER,
    ADD COLUMN IF NOT EXISTS anulada_fecha TIMESTAMP,
    ADD COLUMN IF NOT EXISTS anulada_motivo TEXT;

CREATE INDEX IF NOT EXISTS idx_movimientos_no_anulados 
    ON movimientos_consumo(bien_id) WHERE anulada = FALSE;

COMMIT;

-- ═══════════════════════════════════════════════════════════════════
-- PASO 3: VERIFICAR QUE LAS COLUMNAS SE AGREGARON
-- ═══════════════════════════════════════════════════════════════════

SELECT 
    table_name AS tabla,
    column_name AS columna,
    data_type AS tipo,
    is_nullable AS nullable,
    column_default AS default_value
FROM information_schema.columns
WHERE column_name = 'anulada'
  AND table_schema = 'public'
ORDER BY table_name;

-- Esperado: 7 filas (una por cada tabla con la columna "anulada")

-- ═══════════════════════════════════════════════════════════════════
-- PASO 4: ACTUALIZAR TRIGGERS (mensajes de error)
-- ═══════════════════════════════════════════════════════════════════

BEGIN;

-- ─── MANTENIMIENTOS ───
CREATE OR REPLACE FUNCTION fn_bloquear_delete_mantenimiento()
RETURNS TRIGGER AS $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM mantenimientos 
        WHERE bien_id = OLD.bien_id 
          AND fecha_ingreso > OLD.fecha_ingreso
          AND anulada = FALSE
    ) THEN
        RAISE EXCEPTION 'No se puede eliminar el mantenimiento: hay posteriores. Use "Anular".'
            USING ERRCODE = '23503';
    END IF;
    RETURN OLD;
END;
$$ LANGUAGE plpgsql;

-- ─── ENTRADAS ───
CREATE OR REPLACE FUNCTION fn_bloquear_delete_entrada()
RETURNS TRIGGER AS $$
BEGIN
    IF fn_bien_tiene_movimientos(OLD.bien_id) THEN
        RAISE EXCEPTION 'No se puede eliminar la entrada: el bien tiene movimientos. Use "Anular".'
            USING ERRCODE = '23503';
    END IF;
    RETURN OLD;
END;
$$ LANGUAGE plpgsql;

-- ─── SALIDAS ───
CREATE OR REPLACE FUNCTION fn_bloquear_delete_salida()
RETURNS TRIGGER AS $$
BEGIN
    IF OLD.numero_acta_comite IS NOT NULL 
       AND TRIM(OLD.numero_acta_comite) <> '' THEN
        RAISE EXCEPTION 'No se puede eliminar: tiene Acta Comité (%). Use "Anular".', OLD.numero_acta_comite
            USING ERRCODE = '23503';
    END IF;

    IF OLD.numero_denuncia IS NOT NULL 
       AND TRIM(OLD.numero_denuncia) <> '' THEN
        RAISE EXCEPTION 'No se puede eliminar: tiene Denuncia (%). Use "Anular".', OLD.numero_denuncia
            USING ERRCODE = '23503';
    END IF;

    RETURN OLD;
END;
$$ LANGUAGE plpgsql;

-- ─── TRASLADOS ───
CREATE OR REPLACE FUNCTION fn_bloquear_delete_traslado()
RETURNS TRIGGER AS $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM traslados 
        WHERE bien_id = OLD.bien_id 
          AND fecha_traslado > OLD.fecha_traslado
          AND anulada = FALSE
    ) OR EXISTS (
        SELECT 1 FROM salidas WHERE bien_id = OLD.bien_id AND anulada = FALSE
    ) THEN
        RAISE EXCEPTION 'No se puede eliminar el traslado: hay posteriores. Use "Anular".'
            USING ERRCODE = '23503';
    END IF;
    RETURN OLD;
END;
$$ LANGUAGE plpgsql;

-- ─── PRESTAMOS ───
CREATE OR REPLACE FUNCTION fn_bloquear_delete_prestamo()
RETURNS TRIGGER AS $$
BEGIN
    IF OLD.estado IN ('SOLICITADO', 'APROBADO', 'PRESTADO') THEN
        RAISE EXCEPTION 'No se puede eliminar el préstamo: está activo. Use "Anular".'
            USING ERRCODE = '23503';
    END IF;
    RETURN OLD;
END;
$$ LANGUAGE plpgsql;

-- ─── GARANTIAS ───
CREATE OR REPLACE FUNCTION fn_bloquear_delete_garantia()
RETURNS TRIGGER AS $$
BEGIN
    IF OLD.fecha_fin >= CURRENT_DATE THEN
        RAISE EXCEPTION 'No se puede eliminar la garantía: todavía está vigente. Use "Anular".'
            USING ERRCODE = '23503';
    END IF;
    RETURN OLD;
END;
$$ LANGUAGE plpgsql;

COMMIT;

-- ═══════════════════════════════════════════════════════════════════
-- PASO 5: CREAR VISTAS DE REGISTROS VIGENTES
-- ═══════════════════════════════════════════════════════════════════

BEGIN;

-- ─── MANTENIMIENTOS VIGENTES ───
CREATE OR REPLACE VIEW vista_mantenimientos_vigentes AS
SELECT 
    m.*,
    b.codigo AS bien_codigo,
    b.nombre AS bien_nombre
FROM mantenimientos m
LEFT JOIN bienes b ON b.id = m.bien_id
WHERE m.anulada = FALSE;

-- ─── ENTRADAS VIGENTES ───
CREATE OR REPLACE VIEW vista_entradas_vigentes AS
SELECT 
    e.*,
    b.codigo AS bien_codigo,
    b.nombre AS bien_nombre
FROM entradas e
LEFT JOIN bienes b ON b.id = e.bien_id
WHERE e.anulada = FALSE;

-- ─── SALIDAS VIGENTES ───
CREATE OR REPLACE VIEW vista_salidas_vigentes AS
SELECT 
    s.*,
    b.codigo AS bien_codigo,
    b.nombre AS bien_nombre
FROM salidas s
LEFT JOIN bienes b ON b.id = s.bien_id
WHERE s.anulada = FALSE;

-- ─── TRASLADOS VIGENTES ───
CREATE OR REPLACE VIEW vista_traslados_vigentes AS
SELECT 
    t.*,
    b.codigo AS bien_codigo,
    b.nombre AS bien_nombre
FROM traslados t
LEFT JOIN bienes b ON b.id = t.bien_id
WHERE t.anulada = FALSE;

-- ─── PRESTAMOS VIGENTES ───
CREATE OR REPLACE VIEW vista_prestamos_vigentes AS
SELECT 
    p.*,
    b.codigo AS bien_codigo,
    b.nombre AS bien_nombre
FROM prestamos p
LEFT JOIN bienes b ON b.id = p.bien_id
WHERE p.anulada = FALSE;

-- ─── GARANTIAS VIGENTES ───
CREATE OR REPLACE VIEW vista_garantias_vigentes AS
SELECT 
    g.*,
    b.codigo AS bien_codigo,
    b.nombre AS bien_nombre
FROM garantias g
LEFT JOIN bienes b ON b.id = g.bien_id
WHERE g.anulada = FALSE;

-- ─── MOVIMIENTOS CONSUMO VIGENTES ───
CREATE OR REPLACE VIEW vista_movimientos_consumo_vigentes AS
SELECT 
    m.*,
    b.codigo AS bien_codigo,
    b.nombre AS bien_nombre
FROM movimientos_consumo m
LEFT JOIN bienes b ON b.id = m.bien_id
WHERE m.anulada = FALSE;

COMMIT;

-- ═══════════════════════════════════════════════════════════════════
-- PASO 6: CREAR PERMISOS DE ANULAR
-- ═══════════════════════════════════════════════════════════════════

BEGIN;

INSERT INTO permisos (codigo, descripcion, modulo, activo) VALUES
    ('mantenimiento.anular', 'Anular mantenimiento (soft-delete)', 'mantenimiento', TRUE),
    ('entradas.anular',      'Anular entrada (soft-delete)',      'entradas',      TRUE),
    ('salidas.anular',       'Anular salida (soft-delete)',       'salidas',       TRUE),
    ('traslados.anular',     'Anular traslado (soft-delete)',     'traslados',     TRUE),
    ('prestamos.anular',     'Anular préstamo (soft-delete)',     'prestamos',     TRUE),
    ('garantias.anular',     'Anular garantía (soft-delete)',     'garantias',     TRUE),
    ('consumo.anular',       'Anular movimiento (soft-delete)',   'consumo',       TRUE)
ON CONFLICT (codigo) DO NOTHING;

-- Asignar al rol Administrador
INSERT INTO rol_permisos (rol_id, permiso_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permisos p
WHERE r.nombre = 'Administrador'
  AND p.codigo IN (
    'mantenimiento.anular',
    'entradas.anular',
    'salidas.anular',
    'traslados.anular',
    'prestamos.anular',
    'garantias.anular',
    'consumo.anular'
  )
ON CONFLICT (rol_id, permiso_id) DO NOTHING;

COMMIT;

-- ═══════════════════════════════════════════════════════════════════
-- PASO 7: VERIFICACIÓN FINAL
-- ═══════════════════════════════════════════════════════════════════

-- 7.1 — Columnas agregadas (esperado: 7 filas)
SELECT 
    table_name AS tabla,
    column_name AS columna,
    data_type AS tipo
FROM information_schema.columns
WHERE column_name = 'anulada'
  AND table_schema = 'public'
ORDER BY table_name;

-- 7.2 — Permisos nuevos (esperado: 7 filas)
SELECT 
    p.codigo AS permiso,
    p.modulo,
    r.nombre AS rol_asignado
FROM permisos p
LEFT JOIN rol_permisos rp ON rp.permiso_id = p.id
LEFT JOIN roles r ON r.id = rp.rol_id
WHERE p.codigo LIKE '%.anular'
ORDER BY p.modulo;

-- 7.3 — Vistas creadas (esperado: 7 filas)
SELECT 
    table_name AS vista
FROM information_schema.views
WHERE table_name LIKE 'vista_%_vigentes%'
  AND table_schema = 'public'
ORDER BY table_name;

-- 7.4 — Registros existentes (esperado: todos anulados = 0)
SELECT 
    'mantenimientos' AS tabla,
    COUNT(*) FILTER (WHERE anulada = FALSE) AS vigentes,
    COUNT(*) FILTER (WHERE anulada = TRUE) AS anulados,
    COUNT(*) AS total
FROM mantenimientos
UNION ALL
SELECT 'entradas', 
    COUNT(*) FILTER (WHERE anulada = FALSE),
    COUNT(*) FILTER (WHERE anulada = TRUE),
    COUNT(*) FROM entradas
UNION ALL
SELECT 'salidas', 
    COUNT(*) FILTER (WHERE anulada = FALSE),
    COUNT(*) FILTER (WHERE anulada = TRUE),
    COUNT(*) FROM salidas
UNION ALL
SELECT 'traslados', 
    COUNT(*) FILTER (WHERE anulada = FALSE),
    COUNT(*) FILTER (WHERE anulada = TRUE),
    COUNT(*) FROM traslados
UNION ALL
SELECT 'prestamos', 
    COUNT(*) FILTER (WHERE anulada = FALSE),
    COUNT(*) FILTER (WHERE anulada = TRUE),
    COUNT(*) FROM prestamos
UNION ALL
SELECT 'garantias', 
    COUNT(*) FILTER (WHERE anulada = FALSE),
    COUNT(*) FILTER (WHERE anulada = TRUE),
    COUNT(*) FROM garantias
UNION ALL
SELECT 'movimientos_consumo', 
    COUNT(*) FILTER (WHERE anulada = FALSE),
    COUNT(*) FILTER (WHERE anulada = TRUE),
    COUNT(*) FROM movimientos_consumo
ORDER BY tabla;

-- ═══════════════════════════════════════════════════════════════════
-- FIN DE LA MIGRACIÓN
-- 
-- El sistema sigue funcionando exactamente igual.
-- Ahora tiene las columnas "anulada" listas para la siguiente fase.
-- ═══════════════════════════════════════════════════════════════════