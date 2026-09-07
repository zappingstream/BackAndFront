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
// Agregar User-Agent para evitar errores 503 de YouTube al leer el RSS
httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/115.0.0.0 Safari/537.36");
httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

// 4. Instanciar tu clase tradicionalmente con "new" y ejecutarla
var worker = new ZappingStreamingIncomingVideos.ZappingStreamingIncomingVideos(httpClient, config, logger);

await worker.ExecuteAsync();

Console.WriteLine("Ejecución finalizada.");