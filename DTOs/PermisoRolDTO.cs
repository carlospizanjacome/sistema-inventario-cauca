namespace Almacen.DTOs
{
    public class PermisoRolDTO
    {
        public int PermisoId { get; set; }

        public string Codigo { get; set; } = string.Empty;

        public string? Descripcion { get; set; }

        public string? Modulo { get; set; }   // ← AGREGAR ESTA LÍNEA

        public bool Asignado { get; set; }
    }
}