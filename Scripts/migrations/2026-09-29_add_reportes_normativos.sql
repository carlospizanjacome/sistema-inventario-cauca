-- Matriz de Reportes Normativos - V2.0 seccion 19
-- Fecha: 2026-09-29

CREATE TABLE IF NOT EXISTS configuracion_reportes_normativos (
    id                  SERIAL PRIMARY KEY,
    entidad_receptora   VARCHAR(150) NOT NULL,
    nombre_reporte      VARCHAR(200) NOT NULL,
    descripcion         TEXT,
    sujeto_obligado     VARCHAR(200),
    periodicidad        VARCHAR(20) NOT NULL
        CHECK (periodicidad IN ('MENSUAL','BIMESTRAL','TRIMESTRAL','SEMESTRAL','ANUAL','EVENTUAL')),
    fecha_limite        VARCHAR(80),
    vigencia_desde      DATE,
    vigencia_hasta      DATE,
    version_formato     VARCHAR(30),
    formato_salida      VARCHAR(20)
        CHECK (formato_salida IN ('XLSX','CSV','XML','PDF')),
    campos_requeridos   TEXT,
    reglas_validacion   TEXT,
    fuente_oficial      VARCHAR(300),
    url_oficial         VARCHAR(500),
    responsable_interno VARCHAR(200),
    estado              VARCHAR(20) NOT NULL DEFAULT 'BORRADOR'
        CHECK (estado IN ('BORRADOR','VIGENTE','RETIRADO')),
    plantilla_ruta      VARCHAR(300),
    created_at          TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_rep_norm_estado ON configuracion_reportes_normativos(estado);
CREATE INDEX IF NOT EXISTS idx_rep_norm_periodicidad ON configuracion_reportes_normativos(periodicidad);

INSERT INTO permisos (codigo, descripcion, modulo, activo) VALUES
    ('reportes_normativos.ver',      'Ver reportes normativos',      'reportes_normativos', TRUE),
    ('reportes_normativos.crear',    'Crear reporte normativo',      'reportes_normativos', TRUE),
    ('reportes_normativos.editar',   'Editar reporte normativo',     'reportes_normativos', TRUE),
    ('reportes_normativos.eliminar', 'Eliminar reporte normativo',   'reportes_normativos', TRUE)
ON CONFLICT (codigo) DO NOTHING;
