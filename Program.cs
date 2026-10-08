using Google.Cloud.Firestore;
using Google.Api.Gax;
using Google.Apis.Auth.OAuth2;
using System.Text.Json.Serialization;
using Salas_Elvis.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Threading.RateLimiting;
using Salas_Elvis.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))
    .AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddScoped<ReservaService>();
var demonstracao = builder.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Demo:Enabled");
builder.Services.AddSingleton(new ModoAplicacao(demonstracao));
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = "Salas.Sessao";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
    options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 15, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
if (demonstracao)
    builder.Services.AddSingleton<IEscolaStore>(new LocalDemoStore(Path.GetFullPath(
        builder.Configuration["Demo:DataPath"] ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "demo.json"))));
else
    builder.Services.AddSingleton<IEscolaStore, FirestoreEscolaStore>();
builder.Services.AddSingleton(_ =>
{
    var projectId = builder.Configuration["Firestore:ProjectId"];
    if (string.IsNullOrWhiteSpace(projectId))
        throw new ReservaException(503, "Configure Firestore:ProjectId antes de acessar as reservas.");
    var credentialsPath = builder.Configuration["Firestore:CredentialsPath"];
    return new FirestoreDbBuilder
    {
        ProjectId = projectId,
        GoogleCredential = string.IsNullOrWhiteSpace(credentialsPath) ? null
            : CredentialFactory.FromFile<ServiceAccountCredential>(credentialsPath).ToGoogleCredential(),
        EmulatorDetection = EmulatorDetection.EmulatorOrProduction
    }.Build();
});

var app = builder.Build();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Provisionamento explícito do primeiro acesso no Firestore, sem senhas padrão na nuvem.
if (!demonstracao)
{
    foreach (var perfil in new[] { Perfis.Admin, Perfis.Professor })
    {
        var email = builder.Configuration[$"Bootstrap:{perfil}:Email"];
        var senha = builder.Configuration[$"Bootstrap:{perfil}:Senha"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha)) continue;
        if (senha.Length < 10) throw new InvalidOperationException("A senha inicial deve ter pelo menos 10 caracteres.");
        var nome = builder.Configuration[$"Bootstrap:{perfil}:Nome"] ?? perfil;
        var hash = BCrypt.Net.BCrypt.HashPassword(senha);
        await app.Services.GetRequiredService<IEscolaStore>().AlterarAsync(dados =>
        {
            if (dados.Usuarios.Any(u => string.Equals(u.Email, email.Trim(), StringComparison.OrdinalIgnoreCase))) return false;
            dados.Usuarios.Add(new Usuario { Id = Guid.NewGuid().ToString("N"), Nome = nome,
                Email = email.Trim(), SenhaHash = hash, Perfil = perfil });
            return true;
        });
    }
}
await app.RunAsync();
