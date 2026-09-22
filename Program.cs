using Almacen.Components;
using Almacen.Interfaces;
using Almacen.Models;
using Almacen.Repositories;
using Almacen.Services;

var builder = WebApplication.CreateBuilder(args);

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
QuestPDF.Settings.UseEnvironmentFonts = false;

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<UsuarioSesionService>();
builder.Services.AddScoped<SeguridadService>();
builder.Services.AddScoped<PermisoService>();

builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<IRolRepository, RolRepository>();
builder.Services.AddScoped<IPermisoRepository, PermisoRepository>();
builder.Services.AddScoped<IRolPermisoRepository,RolPermisoRepository>();


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


builder.Services.AddScoped<ArchivoService>();
builder.Services.AddScoped<NpgsqlConnectionFactory>();

builder.Services.AddScoped<ActaPdfService>();

builder.Services.AddScoped<ExcelExportService>();

builder.Services.AddScoped<ConfirmService>();

builder.Services.AddScoped<ToastService>();


Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
var app = builder.Build();


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

// SOLUCIÓN CRÍTICA .NET 9: Habilita la lectura física de la carpeta wwwroot (Estilos CSS)
app.UseStaticFiles();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

//var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
//app.Urls.Add($"http://0.0.0.0:{port}");
//app.Run();

// En producción (Railway) usar puerto dinámico.
// En desarrollo, dejar que VS use launchSettings.json (puerto 7012).
if (!app.Environment.IsDevelopment())
{
    var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
    app.Urls.Add($"http://0.0.0.0:{port}");
}

app.Run();