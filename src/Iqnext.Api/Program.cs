using IQData;
using IQRepositories;
using IQRepositories.Interfaces;
using IQServices;
using IQServices.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// CORS (dev)
const string CorsPolicy = "DevCors";
builder.Services.AddCors(opt =>
{
    opt.AddPolicy(CorsPolicy, policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
        // .AllowCredentials() // solo si usas cookies/autenticación
        );
});

// Bind de cadena de conexión
builder.Services.Configure<DbSettings>(opt =>
    opt.Default = builder.Configuration.GetConnectionString("Default"));

// IoC
builder.Services.AddSingleton<DbConnectionFactory>();
builder.Services.AddScoped<IProcesosRepository, ProcesosRepository>();
builder.Services.AddScoped<IProcesosService, ProcesosService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// Redirección a HTTPS (opcional; si tu front llama por https)
app.UseHttpsRedirection();

// CORS antes de MapControllers
app.UseCors(CorsPolicy);

app.MapControllers();
app.Run();