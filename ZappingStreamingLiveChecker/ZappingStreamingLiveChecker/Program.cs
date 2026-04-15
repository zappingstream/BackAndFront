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
    public class UpcomingVideo
    {
        public string VideoId { get; set; }
        public string Title { get; set; }
        public string ScheduledStartTime { get; set; }
        public string ThumbnailUrl { get; set; }
        public string AddedAt { get; set; }
    }

    // 1. Añadimos el modelo para Actives
    public class ActiveVideo
    {
        public string VideoId { get; set; }
        public string Title { get; set; }
        public string ScheduledStartTime { get; set; }
        public string ThumbnailUrl { get; set; }
        public string AddedAt { get; set; }
    }

    public class FirebaseChannel
    {
        public string ChannelName { get; set; }
        public bool ChannelLive { get; set; }
        public string LiveVideoId { get; set; }
        public string ChannelImgLiveUrl { get; set; }
        public string LastActivityAt { get; set; }

        // Diccionarios para manejar multi-estado
        public Dictionary<string, UpcomingVideo> Upcoming { get; set; }
        public Dictionary<string, ActiveVideo> Actives { get; set; }
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

        // --- MÓDULO 1: LIMPIEZA DE VIVOS MULTI-ESTADO ---
        static async Task EjecutarCazaFantasmasVivos(FirebaseClient firebase, YouTubeService yt)
        {
            Console.WriteLine("\n=== INICIANDO LIMPIEZA DE VIVOS CAÍDOS ===");
            var estadoActualFirebase = await firebase.Child("Channels").OnceAsync<FirebaseChannel>();

            // Filtramos canales que tienen al menos un vivo registrado (o que quedaron trabados en legacy)
            var canalesEnVivo = estadoActualFirebase
                .Where(c => (c.Object.Actives != null && c.Object.Actives.Any()) || c.Object.ChannelLive)
                .ToList();

            if (!canalesEnVivo.Any())
            {
                Console.WriteLine("No hay canales en vivo. Nada que limpiar.");
                return;
            }

            // Recolectamos TODOS los IDs de videos que el sistema cree que están en vivo
            var videoIds = new HashSet<string>();
            foreach (var canal in canalesEnVivo)
            {
                if (canal.Object.Actives != null)
                {
                    foreach (var videoId in canal.Object.Actives.Keys) videoIds.Add(videoId);
                }
                else if (!string.IsNullOrEmpty(canal.Object.LiveVideoId))
                {
                    videoIds.Add(canal.Object.LiveVideoId); // Por si quedó algún canal legacy sin migrar
                }
            }

            var videosVivosRealesEnYt = new HashSet<string>();

            // Consultamos a YouTube (ahora pedimos snippet para verificar el status real)
            foreach (var lote in videoIds.Chunk(50))
            {
                var request = yt.Videos.List("snippet");
                request.Id = string.Join(",", lote);
                var response = await request.ExecuteAsync();

                if (response.Items != null)
                {
                    foreach (var item in response.Items)
                    {
                        // Verificamos que realmente siga transmitiendo en este momento
                        if (item.Snippet?.LiveBroadcastContent == "live")
                        {
                            videosVivosRealesEnYt.Add(item.Id);
                        }
                    }
                }
            }

            // Procesamos cada canal para limpiar sus fantasmas
            foreach (var canal in canalesEnVivo)
            {
                var vivosActuales = canal.Object.Actives ?? new Dictionary<string, ActiveVideo>();

                // Si el canal estaba en legacy sin 'Actives', lo simulamos para procesarlo igual
                if (!vivosActuales.Any() && !string.IsNullOrEmpty(canal.Object.LiveVideoId))
                {
                    vivosActuales[canal.Object.LiveVideoId] = new ActiveVideo { VideoId = canal.Object.LiveVideoId };
                }

                bool huboCambios = false;
                var vivosSobrevivientes = new List<ActiveVideo>();

                foreach (var kvp in vivosActuales)
                {
                    if (!videosVivosRealesEnYt.Contains(kvp.Key))
                    {
                        // Es un fantasma. Lo borramos de la subcarpeta Actives.
                        Console.WriteLine($"- {canal.Key}: Matando stream fantasma {kvp.Key}...");
                        await firebase.Child("Channels").Child(canal.Key).Child("Actives").Child(kvp.Key).DeleteAsync();
                        huboCambios = true;
                    }
                    else
                    {
                        vivosSobrevivientes.Add(kvp.Value);
                    }
                }

                if (huboCambios)
                {
                    // LÓGICA DE FALLBACK AUTOMÁTICO
                    if (vivosSobrevivientes.Any())
                    {
                        var fallbackVideo = vivosSobrevivientes.OrderByDescending(v => v.AddedAt ?? "").First();
                        Console.WriteLine($"> {canal.Key}: Sobreviven otros streams. Fallback a {fallbackVideo.VideoId}");

                        await firebase.Child("Channels").Child(canal.Key).PatchAsync(new
                        {
                            LiveVideoId = fallbackVideo.VideoId,
                            ChannelImgLiveUrl = fallbackVideo.ThumbnailUrl ?? canal.Object.ChannelImgLiveUrl,
                            LastActivityAt = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                        });
                    }
                    else
                    {
                        Console.WriteLine($"> {canal.Key}: No quedaron streams vivos. APAGANDO CANAL.");
                        await firebase.Child("Channels").Child(canal.Key).PatchAsync(new
                        {
                            ChannelLive = false,
                            LiveVideoId = "",
                            ChannelImgLiveUrl = "",
                            LastActivityAt = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                        });
                    }
                }
            }

            Console.WriteLine("=== LIMPIEZA DE VIVOS FINALIZADA ===");
        }

        // --- MÓDULO 2: EL NUEVO REVISOR DE UPCOMING ---
        static async Task EjecutarCazaFantasmasUpcoming(FirebaseClient firebase, YouTubeService yt)
        {
            Console.WriteLine("\n=== INICIANDO REVISIÓN DE UPCOMING ATRASADOS ===");
            var estadoActualFirebase = await firebase.Child("Channels").OnceAsync<FirebaseChannel>();

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
                            if (scheduledTime <= ahora)
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

            foreach (var lote in videoIds.Chunk(50))
            {
                var request = yt.Videos.List("snippet,status,liveStreamingDetails,contentDetails");
                request.Id = string.Join(",", lote);
                var response = await request.ExecuteAsync();

                if (response.Items != null)
                {
                    foreach (var item in response.Items) infoDeYouTube[item.Id] = item;
                }
            }

            foreach (var item in upcomingVencidos)
            {
                var ytVideo = infoDeYouTube.ContainsKey(item.Video.VideoId) ? infoDeYouTube[item.Video.VideoId] : null;
                string status = ytVideo?.Snippet?.LiveBroadcastContent ?? "none";

                var canalRef = firebase.Child("Channels").Child(item.ChannelKey);
                var upcomingRef = canalRef.Child("Upcoming").Child(item.Video.VideoId);
                var activeRef = canalRef.Child("Actives").Child(item.Video.VideoId); // REFERENCIA A LA NUEVA CARPETA

                bool esEstreno = ytVideo?.LiveStreamingDetails != null && ytVideo?.LiveStreamingDetails?.ConcurrentViewers == null &&
                                 ytVideo?.ContentDetails != null && !(ytVideo?.ContentDetails?.Duration == "P0D" || ytVideo?.ContentDetails?.Duration == "PT0D");

                if (esEstreno && (status == "upcoming" || status == "live"))
                {
                    Console.WriteLine($"- {item.ChannelKey}: El video {item.Video.VideoId} es un ESTRENO PREGRABADO. Eliminándolo...");
                    await upcomingRef.DeleteAsync();
                    continue;
                }

                if (status == "live")
                {
                    Console.WriteLine($"- {item.ChannelKey}: ¡El programado {item.Video.VideoId} está EN VIVO! Actualizando...");

                    // 1. LO AGREGAMOS A LA COLECCIÓN DE ACTIVOS
                    await activeRef.PutAsync(new ActiveVideo
                    {
                        VideoId = item.Video.VideoId,
                        Title = item.Video.Title,
                        ScheduledStartTime = item.Video.ScheduledStartTime,
                        ThumbnailUrl = item.Video.ThumbnailUrl,
                        AddedAt = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                    });

                    // 2. ACTUALIZAMOS EL ESTADO LEGACY DEL CANAL
                    await canalRef.PatchAsync(new
                    {
                        ChannelLive = true,
                        LiveVideoId = item.Video.VideoId,
                        ChannelImgLiveUrl = item.Video.ThumbnailUrl,
                        LastActivityAt = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                    });

                    // 3. LO BORRAMOS DE UPCOMING
                    await upcomingRef.DeleteAsync();
                }
                else if (status == "none")
                {
                    Console.WriteLine($"- {item.ChannelKey}: El programado {item.Video.VideoId} fue cancelado o no existe. Borrando...");
                    await upcomingRef.DeleteAsync();
                }
                else if (status == "upcoming")
                {
                    if (DateTimeOffset.TryParse(item.Video.ScheduledStartTime, out var scheduledTime))
                    {
                        var horasAtrasado = (ahora - scheduledTime).TotalHours;

                        if (horasAtrasado > 24)
                        {
                            Console.WriteLine($"- {item.ChannelKey}: El programado {item.Video.VideoId} superó las 24hs de gracia sin prender. Eliminándolo...");
                            await upcomingRef.DeleteAsync();
                        }
                        else
                        {
                            Console.WriteLine($"- {item.ChannelKey}: El programado {item.Video.VideoId} sigue en espera ({Math.Round(horasAtrasado, 1)} hs de atraso).");
                        }
                    }
                }
            }
        }
    }
}