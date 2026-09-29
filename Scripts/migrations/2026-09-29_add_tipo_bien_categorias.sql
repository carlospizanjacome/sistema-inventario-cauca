-- Agrega tipo_bien a categorias + backfill por CGN
-- Fecha: 2026-09-29
-- Contexto: rediseno modulo Categorias (BUG #31)

ALTER TABLE categorias 
ADD COLUMN IF NOT EXISTS tipo_bien VARCHAR(20) 
    CHECK (tipo_bien IN ('devolutivo', 'consumo', 'ambos'));

UPDATE categorias 
SET tipo_bien = CASE 
    WHEN codigo_cgn LIKE '1635%' THEN 'devolutivo'
    WHEN codigo_cgn = '1905'     THEN 'consumo'
    ELSE 'devolutivo'
END
WHERE tipo_bien IS NULL;

ALTER TABLE categorias 
ALTER COLUMN tipo_bien SET DEFAULT 'devolutivo';

ALTER TABLE categorias 
ALTER COLUMN tipo_bien SET NOT NULL;
