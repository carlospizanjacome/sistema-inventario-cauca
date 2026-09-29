using System.Globalization;
using Almacen.Components;
using Almacen.Interfaces;
using Almacen.Models;
using Almacen.Repositories;
using Almacen.Services;

// ═══ CULTURA GLOBAL: $12.000 · 12.345,67 · 1.234 ═══
var culturaCO = (CultureInfo)CultureInfo.GetCultureInfo("es-CO").Clone();
culturaCO.NumberFormat.CurrencyPositivePattern = 0;
culturaCO.NumberFormat.CurrencyNegativePattern = 1;
CultureInfo.DefaultThreadCurrentCulture = culturaCO;
CultureInfo.DefaultThreadCurrentUICulture = culturaCO;

var builder = WebApplication.CreateBuilder(args);

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
QuestPDF.Settings.UseSystemFonts = false;

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<Microsoft.AspNetCore.SignalR.HubOptions>(options =>
{
    options.MaximumReceiveMessageSize = 50 * 1024 * 1024; // 50 MB
});

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<UsuarioSesionService>();
builder.Services.AddScoped<SeguridadService>();
builder.Services.AddScoped<PermisoService>();

builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<IRolRepository, RolRepository>();
builder.Services.AddScoped<IPermisoRepository, PermisoRepository>();
builder.Services.AddScoped<IRolPermisoRepository, RolPermisoRepository>();
builder.Services.AddScoped<ICategoriaRepository, CategoriaRepository>();
builder.Services.AddScoped<IBienRepository, BienRepository>();
builder.Services.AddScoped<IDashboardRepository, DashboardRepository>();
builder.Services.AddScoped<IInstitucionRepository, InstitucionRepository>();
builder.Services.AddScoped<ISedeRepository, SedeRepository>();
builder.Services.AddScoped<IBloqueRepository, BloqueRepository>();
builder.Services.AddScoped<IAulaRepository, AulaRepository>();
builder.Services.AddScoped<IFuncionarioRepository, FuncionarioRepository>();
builder.Services.AddScoped<IEntradaRepository, EntradaRepository>();
builder.Services.AddScoped<IProveedorRepository, ProveedorRepository>();
builder.Services.AddScoped<ISalidaRepository, SalidaRepository>();
builder.Services.AddScoped<ITrasladoRepository, TrasladoRepository>();
builder.Services.AddScoped<IDepreciacionRepository, DepreciacionRepository>();
builder.Services.AddScoped<DepreciacionService>();
builder.Services.AddScoped<IVidaUtilRepository, VidaUtilRepository>();
builder.Services.AddScoped<IKardexRepository, KardexRepository>();
builder.Services.AddScoped<IReporteRepository, ReporteRepository>();
builder.Services.AddScoped<ITomaFisicaRepository, TomaFisicaRepository>();
builder.Services.AddScoped<IExpedienteRepository, ExpedienteRepository>();
builder.Services.AddScoped<IPrestamoRepository, PrestamoRepository>();
builder.Services.AddScoped<IMantenimientoRepository, MantenimientoRepository>();
builder.Services.AddScoped<IGarantiaRepository, GarantiaRepository>();

builder.Services.AddScoped<QrService>();
builder.Services.AddScoped<ArchivoService>();
builder.Services.AddScoped<NpgsqlConnectionFactory>();
builder.Services.AddScoped<ActaPdfService>();
builder.Services.AddScoped<ExcelExportService>();
builder.Services.AddScoped<ConfirmService>();
builder.Services.AddScoped<ToastService>();

Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

if (!app.Environment.IsDevelopment())
{
    var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
    app.Urls.Add($"http://0.0.0.0:{port}");
}

app.Run();