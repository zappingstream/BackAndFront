using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using ZappingStreamingIncomingVideos;

Console.WriteLine("Iniciando aplicación (configuración por variables de entorno)...");

// 1. Cargar la configuración SOLAMENTE desde variables de entorno
var config = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();

// 2. Configurar el Logger manualmente
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConfiguration(config.GetSection("Logging"));
    builder.AddConsole();
});
var logger = loggerFactory.CreateLogger<ZappingStreamingIncomingVideos.ZappingStreamingIncomingVideos>();

// 3. Crear el HttpClient manualmente
using var httpClient = new HttpClient();

// 4. Instanciar tu clase tradicionalmente con "new" y ejecutarla
var worker = new ZappingStreamingIncomingVideos.ZappingStreamingIncomingVideos(httpClient, config, logger);

await worker.ExecuteAsync();

Console.WriteLine("Ejecución finalizada.");