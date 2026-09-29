-- Modulo de Prestamos - V2.0 seccion 7.8
-- Fecha: 2026-09-29

CREATE TABLE IF NOT EXISTS prestamos (
    id                          SERIAL PRIMARY KEY,
    bien_id                     INTEGER NOT NULL REFERENCES bienes(id) ON DELETE RESTRICT,
    institucion_id              INTEGER NOT NULL REFERENCES instituciones(id),
    funcionario_solicita_id     INTEGER NOT NULL REFERENCES funcionarios(id),
    funcionario_aprueba_id      INTEGER NULL REFERENCES funcionarios(id),
    fecha_solicitud             DATE NOT NULL,
    fecha_aprobacion            DATE NULL,
    fecha_prestamo              DATE NULL,
    fecha_devolucion_prevista   DATE NOT NULL,
    fecha_devolucion_real       DATE NULL,
    estado                      VARCHAR(20) NOT NULL DEFAULT 'SOLICITADO'
        CHECK (estado IN ('SOLICITADO','APROBADO','PRESTADO','DEVUELTO','RECHAZADO','VENCIDO','ANULADO')),
    motivo                      TEXT NOT NULL,
    observaciones_entrega       TEXT,
    observaciones_devolucion    TEXT,
    estado_bien_entrega         VARCHAR(20),
    estado_bien_devolucion      VARCHAR(20),
    created_at                  TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at                  TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_prestamos_institucion ON prestamos(institucion_id);
CREATE INDEX IF NOT EXISTS idx_prestamos_bien ON prestamos(bien_id);
CREATE INDEX IF NOT EXISTS idx_prestamos_estado ON prestamos(estado);
CREATE INDEX IF NOT EXISTS idx_prestamos_fecha_prev ON prestamos(fecha_devolucion_prevista);

INSERT INTO permisos (codigo, descripcion, modulo, activo) VALUES
    ('prestamos.ver',      'Ver prestamos',               'prestamos', TRUE),
    ('prestamos.crear',    'Crear solicitud de prestamo', 'prestamos', TRUE),
    ('prestamos.aprobar',  'Aprobar / Rechazar prestamos','prestamos', TRUE),
    ('prestamos.entregar', 'Registrar entrega',           'prestamos', TRUE),
    ('prestamos.devolver', 'Registrar devolucion',        'prestamos', TRUE),
    ('prestamos.eliminar', 'Eliminar prestamos anulados', 'prestamos', TRUE)
ON CONFLICT (codigo) DO NOTHING;
