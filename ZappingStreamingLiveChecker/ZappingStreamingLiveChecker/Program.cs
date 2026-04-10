using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Firebase.Database;
using Firebase.Database.Query;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;

namespace ZappingGhostBusterConsole
{
    // Clase modelo para leer de Firebase
    public class FirebaseChannel
    {
        public string ChannelName { get; set; }
        public bool ChannelLive { get; set; }
        public string LiveVideoId { get; set; }
        public string ChannelImgLiveUrl { get; set; }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== INICIANDO CAZA DE FANTASMAS (GITHUB ACTIONS) ===");

            // 1. Leer secretos desde las variables de entorno de GitHub Actions
            string firebaseUrl = "https://zappingstreaming-default-rtdb.firebaseio.com/";
            string ytApiKey = Environment.GetEnvironmentVariable("YOUTUBE_APIKEY") ?? "";
            string firebaseSecret = Environment.GetEnvironmentVariable("FIREBASE_SECRET") ?? "";

            if (string.IsNullOrEmpty(ytApiKey) || string.IsNullOrEmpty(firebaseSecret))
            {
                Console.WriteLine("ERROR FATAL: Faltan las variables de entorno YOUTUBE_API_KEY o FIREBASE_SECRET.");
                Environment.Exit(1); // Falla el Action si no hay credenciales
            }

            // 2. Inicializar clientes
            var firebaseClient = new FirebaseClient(firebaseUrl, new FirebaseOptions
            {
                AuthTokenAsyncFactory = () => Task.FromResult(firebaseSecret)
            });

            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = ytApiKey,
                ApplicationName = "ZappingGhostBuster"
            });

            try
            {
                Console.WriteLine("Paso 1: Buscando canales 'En Vivo' en Firebase...");
                var estadoActualFirebase = await firebaseClient.Child("Channels").OnceAsync<FirebaseChannel>();

                var canalesEnVivo = estadoActualFirebase
                    .Where(c => c.Object.ChannelLive && !string.IsNullOrWhiteSpace(c.Object.LiveVideoId))
                    .ToList();

                if (!canalesEnVivo.Any())
                {
                    Console.WriteLine("No hay canales en vivo en la base de datos. Nada que limpiar.");
                    return; // Termina el programa exitosamente
                }

                Console.WriteLine($"Se encontraron {canalesEnVivo.Count} canales en vivo. Consultando a YouTube...");

                var videoIds = canalesEnVivo.Select(c => c.Object.LiveVideoId).Distinct().ToList();
                var lotesDeVideos = videoIds.Chunk(50);
                var videosConfirmadosPorYt = new HashSet<string>();

                // Paso 2: Consultar a YouTube en bloques de 50
                foreach (var lote in lotesDeVideos)
                {
                    var request = youtubeService.Videos.List("id");
                    request.Id = string.Join(",", lote);
                    var ytResponse = await request.ExecuteAsync();

                    if (ytResponse.Items != null)
                    {
                        foreach (var item in ytResponse.Items)
                        {
                            videosConfirmadosPorYt.Add(item.Id);
                        }
                    }
                }

                // Paso 3: Detectar fantasmas (están en Firebase pero NO en YouTube)
                var canalesFantasmas = canalesEnVivo
                    .Where(c => !videosConfirmadosPorYt.Contains(c.Object.LiveVideoId))
                    .ToList();

                if (!canalesFantasmas.Any())
                {
                    Console.WriteLine("Todos los directos están sanos y online. No se detectaron fantasmas.");
                    return; // Termina el programa exitosamente
                }

                Console.WriteLine($"¡ALERTA! Se detectaron {canalesFantasmas.Count} directos caídos. Limpiando Firebase...");

                // Paso 4: Apagar los canales caídos
                foreach (var fantasma in canalesFantasmas)
                {
                    Console.WriteLine($"- Apagando: {fantasma.Object.ChannelName ?? fantasma.Key} (Video ID: {fantasma.Object.LiveVideoId})");

                    var updateData = new
                    {
                        ChannelLive = false,
                        LiveVideoId = "",
                        ChannelImgLiveUrl = ""
                    };

                    await firebaseClient
                        .Child("Channels")
                        .Child(fantasma.Key)
                        .PatchAsync(updateData);
                }

                // Opcional: Dejar constancia en Firebase de la última pasada
                await firebaseClient.Child("Meta").PatchAsync(new { LastGhostCheck = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") });

                Console.WriteLine("=== LIMPIEZA FINALIZADA CON ÉXITO ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR CRÍTICO: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Environment.Exit(1); // Hace que el step de GitHub Actions marque "Failed"
            }
        }
    }
}