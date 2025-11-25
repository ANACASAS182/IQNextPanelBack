using System.Net;                    // 👈 para ForwardedHeaders
using Microsoft.AspNetCore.HttpOverrides;

using IQData;
using IQRepositories;
using IQRepositories.Interfaces;
using IQServices;
using IQServices.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// CORS
const string CorsPolicy = "IqnextCors";
var allowedOrigins = new[]
{
    "http://localhost:4200",                 // dev
    "https://automatizacion.iqnext.ai",      // prod (tu front público)
    "https://www.automatizacion.iqnext.ai"   // por si acaso
};

builder.Services.AddCors(opt =>
{
    opt.AddPolicy(CorsPolicy, policy =>
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
    // .AllowCredentials() 
    );
});

builder.Services.Configure<DbSettings>(opt =>
    opt.Default = builder.Configuration.GetConnectionString("Default"));

builder.Services.AddSingleton<DbConnectionFactory>();
builder.Services.AddScoped<IProcesosRepository, ProcesosRepository>();
builder.Services.AddScoped<IProcesosService, ProcesosService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto
});

app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();
app.UseCors(CorsPolicy);

app.MapControllers();
app.Run();
