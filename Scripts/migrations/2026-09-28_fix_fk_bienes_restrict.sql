ALTER TABLE depreciacion
DROP CONSTRAINT IF EXISTS depreciacion_bien_id_fkey,
ADD CONSTRAINT depreciacion_bien_id_fkey
    FOREIGN KEY (bien_id) REFERENCES bienes(id) ON DELETE RESTRICT;

ALTER TABLE toma_fisica_detalle
DROP CONSTRAINT IF EXISTS toma_fisica_detalle_bien_id_fkey,
ADD CONSTRAINT toma_fisica_detalle_bien_id_fkey
    FOREIGN KEY (bien_id) REFERENCES bienes(id) ON DELETE RESTRICT;
