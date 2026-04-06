using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using ZappingStreamingDBService; // Tu namespace

// 1. Leemos el entorno actual (quitamos el forzado a Development para que GitHub Actions pueda usar Production)
var entorno = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";

using IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((hostingContext, config) =>
    {
        // 2. Limpiamos la magia por defecto y le decimos exactamente dónde buscar
        config.Sources.Clear();

        // AppDomain.CurrentDomain.BaseDirectory es infalible: apunta a la carpeta bin donde está el .exe
        config.SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
              .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
              .AddJsonFile($"appsettings.{entorno}.json", optional: true, reloadOnChange: true)
	          .AddEnvironmentVariables();
    })
    .ConfigureServices((hostContext, services) =>
    {
        services.AddHttpClient();
        services.AddHostedService<ZappingStreamingDBService.ZappingStreamingDBService>();
    })
    .Build();

Console.WriteLine($"Iniciando en entorno: {entorno}...");
await host.RunAsync();