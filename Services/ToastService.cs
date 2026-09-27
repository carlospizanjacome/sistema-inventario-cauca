namespace Almacen.Services;

public enum ToastTipo { Success, Danger, Warning, Info }

public class ToastMensaje
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Texto { get; set; } = string.Empty;
    public ToastTipo Tipo { get; set; }
}

public class ToastService
{
    public event Action<ToastMensaje>? OnShow;

    public void Exito(string mensaje) => Mostrar(mensaje, ToastTipo.Success);
    public void Error(string mensaje) => Mostrar(mensaje, ToastTipo.Danger);
    public void Warning(string mensaje) => Mostrar(mensaje, ToastTipo.Warning);
    public void Info(string mensaje) => Mostrar(mensaje, ToastTipo.Info);

    private void Mostrar(string mensaje, ToastTipo tipo)
    {
        Console.WriteLine($"[ToastService] Mostrar() → {tipo}: {mensaje}");
        Console.WriteLine($"[ToastService] Suscriptores: {OnShow?.GetInvocationList().Length ?? 0}");
        Console.WriteLine($"[ToastService] HashCode: {GetHashCode()}");
        OnShow?.Invoke(new ToastMensaje { Texto = mensaje, Tipo = tipo });
    }
}