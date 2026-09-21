namespace Almacen.Services;

public class ConfirmOpciones
{
    public string Titulo { get; set; } = "Confirmar acción";
    public string Mensaje { get; set; } = string.Empty;
    public string TextoConfirmar { get; set; } = "Aceptar";
    public string TextoCancelar { get; set; } = "Cancelar";
    public string Icono { get; set; } = "bi-question-circle-fill";
    public string ColorBoton { get; set; } = "#4f46e5"; // indigo por defecto
    public bool EsPeligroso { get; set; } = false;
}

public class ConfirmService
{
    public event Func<ConfirmOpciones, Task<bool>>? OnAsk;

    public async Task<bool> Preguntar(ConfirmOpciones opciones)
    {
        if (OnAsk is null) return false;
        return await OnAsk.Invoke(opciones);
    }

    public Task<bool> Preguntar(string mensaje, string titulo = "Confirmar acción")
        => Preguntar(new ConfirmOpciones { Mensaje = mensaje, Titulo = titulo });

    public Task<bool> Peligroso(string mensaje, string titulo = "Confirmar eliminación")
        => Preguntar(new ConfirmOpciones
        {
            Mensaje = mensaje,
            Titulo = titulo,
            Icono = "bi-exclamation-triangle-fill",
            ColorBoton = "#dc2626",
            EsPeligroso = true,
            TextoConfirmar = "Sí, eliminar"
        });
}