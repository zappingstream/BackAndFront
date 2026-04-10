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
    // 1. Añadimos el modelo para leer la subcarpeta Upcoming
    public class UpcomingVideo
    {
        public string VideoId { get; set; }
        public string Title { get; set; }
        public string ScheduledStartTime { get; set; }
        public string ThumbnailUrl { get; set; }
    }

    public class FirebaseChannel
    {
        public string ChannelName { get; set; }
        public bool ChannelLive { get; set; }
        public string LiveVideoId { get; set; }
        public string ChannelImgLiveUrl { get; set; }
        // Agregamos un diccionario para leer los Upcoming de un plumazo
        public Dictionary<string, UpcomingVideo> Upcoming { get; set; }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            string firebaseUrl = "https://zappingstreaming-default-rtdb.firebaseio.com/";
            string ytApiKey = Environment.GetEnvironmentVariable("YOUTUBE_APIKEY") ?? "";
            string firebaseSecret = Environment.GetEnvironmentVariable("FIREBASE_SECRET") ?? "";

            if (string.IsNullOrEmpty(ytApiKey) || string.IsNullOrEmpty(firebaseSecret))
            {
                Console.WriteLine("ERROR FATAL: Faltan las variables de entorno.");
                Environment.Exit(1);
            }

            var firebaseClient = new FirebaseClient(firebaseUrl, new FirebaseOptions
            {
                AuthTokenAsyncFactory = () => Task.FromResult(firebaseSecret)
            });

            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = ytApiKey,
                ApplicationName = "ZappingGhostBuster"
            });

            // 2. Leemos el argumento para saber qué modo ejecutar
            string modo = args.Length > 0 ? args[0].ToLower() : "all";

            try
            {
                if (modo == "vivos" || modo == "all")
                {
                    await EjecutarCazaFantasmasVivos(firebaseClient, youtubeService);
                }

                if (modo == "upcoming" || modo == "all")
                {
                    await EjecutarCazaFantasmasUpcoming(firebaseClient, youtubeService);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR CRÍTICO: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Environment.Exit(1);
            }
        }

        // --- MÓDULO 1: TU CÓDIGO ORIGINAL (Ligeramente encapsulado) ---
        static async Task EjecutarCazaFantasmasVivos(FirebaseClient firebase, YouTubeService yt)
        {
            Console.WriteLine("\n=== INICIANDO LIMPIEZA DE VIVOS CAÍDOS ===");
            var estadoActualFirebase = await firebase.Child("Channels").OnceAsync<FirebaseChannel>();

            var canalesEnVivo = estadoActualFirebase
                .Where(c => c.Object.ChannelLive && !string.IsNullOrWhiteSpace(c.Object.LiveVideoId))
                .ToList();

            if (!canalesEnVivo.Any())
            {
                Console.WriteLine("No hay canales en vivo. Nada que limpiar.");
                return;
            }

            var videoIds = canalesEnVivo.Select(c => c.Object.LiveVideoId).Distinct().ToList();
            var videosConfirmadosPorYt = new HashSet<string>();

            foreach (var lote in videoIds.Chunk(50))
            {
                var request = yt.Videos.List("id");
                request.Id = string.Join(",", lote);
                var response = await request.ExecuteAsync();

                if (response.Items != null)
                {
                    foreach (var item in response.Items) videosConfirmadosPorYt.Add(item.Id);
                }
            }

            var canalesFantasmas = canalesEnVivo.Where(c => !videosConfirmadosPorYt.Contains(c.Object.LiveVideoId)).ToList();

            if (!canalesFantasmas.Any())
            {
                Console.WriteLine("Todos los directos están sanos.");
                return;
            }

            foreach (var fantasma in canalesFantasmas)
            {
                Console.WriteLine($"- Apagando: {fantasma.Key}");
                await firebase.Child("Channels").Child(fantasma.Key).PatchAsync(new
                {
                    ChannelLive = false,
                    LiveVideoId = "",
                    ChannelImgLiveUrl = ""
                });
            }
        }

        // --- MÓDULO 2: EL NUEVO REVISOR DE UPCOMING ---
        static async Task EjecutarCazaFantasmasUpcoming(FirebaseClient firebase, YouTubeService yt)
        {
            Console.WriteLine("\n=== INICIANDO REVISIÓN DE UPCOMING ATRASADOS ===");
            var estadoActualFirebase = await firebase.Child("Channels").OnceAsync<FirebaseChannel>();

            // Extraer todos los upcoming que ya se pasaron de la hora
            var upcomingVencidos = new List<(string ChannelKey, UpcomingVideo Video)>();
            var ahora = DateTimeOffset.UtcNow;

            foreach (var canal in estadoActualFirebase)
            {
                if (canal.Object.Upcoming != null)
                {
                    foreach (var upc in canal.Object.Upcoming)
                    {
                        if (DateTimeOffset.TryParse(upc.Value.ScheduledStartTime, out var scheduledTime))
                        {
                            if (scheduledTime <= ahora) // ¡Ya debería haber empezado!
                            {
                                upcomingVencidos.Add((canal.Key, upc.Value));
                            }
                        }
                    }
                }
            }

            if (!upcomingVencidos.Any())
            {
                Console.WriteLine("No hay directos programados atrasados que revisar.");
                return;
            }

            Console.WriteLine($"Revisando {upcomingVencidos.Count} videos programados vencidos...");

            var videoIds = upcomingVencidos.Select(u => u.Video.VideoId).Distinct().ToList();
            var infoDeYouTube = new Dictionary<string, Google.Apis.YouTube.v3.Data.Video>();

            // Consultar a YouTube en bloques
            foreach (var lote in videoIds.Chunk(50))
            {
                var request = yt.Videos.List("snippet");
                request.Id = string.Join(",", lote);
                var response = await request.ExecuteAsync();

                if (response.Items != null)
                {
                    foreach (var item in response.Items) infoDeYouTube[item.Id] = item;
                }
            }

            // Procesar los resultados
            foreach (var item in upcomingVencidos)
            {
                var ytVideo = infoDeYouTube.ContainsKey(item.Video.VideoId) ? infoDeYouTube[item.Video.VideoId] : null;
                string status = ytVideo?.Snippet?.LiveBroadcastContent ?? "none";

                var canalRef = firebase.Child("Channels").Child(item.ChannelKey);
                var upcomingRef = canalRef.Child("Upcoming").Child(item.Video.VideoId);

                if (status == "live")
                {
                    // ¡Empezó! Lo pasamos a vivo y lo borramos de upcoming
                    Console.WriteLine($"- {item.ChannelKey}: ¡El programado {item.Video.VideoId} está EN VIVO! Actualizando...");
                    await canalRef.PatchAsync(new
                    {
                        ChannelLive = true,
                        LiveVideoId = item.Video.VideoId,
                        ChannelImgLiveUrl = item.Video.ThumbnailUrl,
                        LastActivityAt = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                    });
                    await upcomingRef.DeleteAsync();
                }
                else if (status == "none")
                {
                    // Fue cancelado, borrado, o ya terminó
                    Console.WriteLine($"- {item.ChannelKey}: El programado {item.Video.VideoId} fue cancelado o no existe. Borrando...");
                    await upcomingRef.DeleteAsync();
                }
                else if (status == "upcoming")
                {
                    // El creador viene tarde, no hacemos nada, lo dejamos en la lista.
                    Console.WriteLine($"- {item.ChannelKey}: El programado {item.Video.VideoId} sigue en espera (Creador atrasado).");
                }
            }
        }
    }
}