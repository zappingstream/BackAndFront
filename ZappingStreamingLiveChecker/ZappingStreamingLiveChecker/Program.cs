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
        public bool IsPremiere { get; set; } // <-- NUEVO
    }

    public class ActiveVideo
    {
        public string VideoId { get; set; }
        public string Title { get; set; }
        public string ScheduledStartTime { get; set; }
        public string ThumbnailUrl { get; set; }
        public string AddedAt { get; set; }
        public bool IsPremiere { get; set; } // <-- NUEVO
    }

    public class FirebaseChannel
    {
        public string ChannelName { get; set; }
        public bool ChannelLive { get; set; }
        public string LiveVideoId { get; set; }
        public string ChannelImgLiveUrl { get; set; }
        public string LastActivityAt { get; set; }
        public bool IsPremiere { get; set; } // <-- NUEVO

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

            try
            {
                await EjecutarCazaFantasmasUnificado(firebaseClient, youtubeService);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR CRÍTICO: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Environment.Exit(1);
            }
        }

        static async Task EjecutarCazaFantasmasUnificado(FirebaseClient firebase, YouTubeService yt)
        {
            Console.WriteLine("\n=== INICIANDO REVISIÓN UNIFICADA (VIVOS + UPCOMING) ===");
            var estadoActualFirebase = await firebase.Child("Channels").OnceAsync<FirebaseChannel>();

            var ahora = DateTimeOffset.UtcNow;
            var videoIds = new HashSet<string>();

            // 1. RECOLECTAR TODOS LOS IDs EN UNA SOLA BOLSA
            foreach (var canal in estadoActualFirebase)
            {
                // Extraer de Vivos
                if (canal.Object.Actives != null)
                {
                    foreach (var key in canal.Object.Actives.Keys) videoIds.Add(key);
                }
                else if (!string.IsNullOrEmpty(canal.Object.LiveVideoId))
                {
                    videoIds.Add(canal.Object.LiveVideoId);
                }

                // Extraer de Upcoming (solo los que ya pasaron su hora)
                if (canal.Object.Upcoming != null)
                {
                    foreach (var upc in canal.Object.Upcoming)
                    {
                        if (DateTimeOffset.TryParse(upc.Value.ScheduledStartTime, out var scheduledTime) && scheduledTime <= ahora)
                        {
                            videoIds.Add(upc.Key);
                        }
                    }
                }
            }

            if (!videoIds.Any())
            {
                Console.WriteLine("No hay videos activos ni directos atrasados para revisar.");
                return;
            }

            Console.WriteLine($"Se evaluarán {videoIds.Count} videos en total...");

            // 2. CONSULTAR A YOUTUBE (Pidiendo todas las partes necesarias a la vez)
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

            // 3. PROCESAR Y ACTUALIZAR CANAL POR CANAL
            foreach (var canal in estadoActualFirebase)
            {
                var canalRef = firebase.Child("Channels").Child(canal.Key);
                bool huboCambios = false;

                // Preparar colección actual de vivos
                var vivosActuales = canal.Object.Actives ?? new Dictionary<string, ActiveVideo>();
                if (!vivosActuales.Any() && !string.IsNullOrEmpty(canal.Object.LiveVideoId))
                {
                    vivosActuales[canal.Object.LiveVideoId] = new ActiveVideo { VideoId = canal.Object.LiveVideoId };
                }

                var vivosSobrevivientes = new List<ActiveVideo>();

                // --- A. LIMPIEZA DE VIVOS ---
                foreach (var kvp in vivosActuales.ToList())
                {
                    var ytVideo = infoDeYouTube.ContainsKey(kvp.Key) ? infoDeYouTube[kvp.Key] : null;
                    if (ytVideo == null || ytVideo.Snippet?.LiveBroadcastContent != "live")
                    {
                        Console.WriteLine($"- {canal.Key}: Matando stream fantasma {kvp.Key}...");
                        await canalRef.Child("Actives").Child(kvp.Key).DeleteAsync();
                        huboCambios = true;
                    }
                    else
                    {
                        vivosSobrevivientes.Add(kvp.Value);
                    }
                }

                // --- B. REVISIÓN DE UPCOMING ---
                if (canal.Object.Upcoming != null)
                {
                    foreach (var upc in canal.Object.Upcoming)
                    {
                        if (DateTimeOffset.TryParse(upc.Value.ScheduledStartTime, out var scheduledTime) && scheduledTime <= ahora)
                        {
                            var ytVideo = infoDeYouTube.ContainsKey(upc.Key) ? infoDeYouTube[upc.Key] : null;
                            string status = ytVideo?.Snippet?.LiveBroadcastContent ?? "none";
                            var upcomingRef = canalRef.Child("Upcoming").Child(upc.Key);

                            if (status == "live")
                            {
                                Console.WriteLine($"- {canal.Key}: ¡El programado {upc.Key} está EN VIVO! Movido a Actives.");
                                bool tieneDuracion = ytVideo?.ContentDetails != null &&
                                                     ytVideo.ContentDetails.Duration != "P0D" &&
                                                     ytVideo.ContentDetails.Duration != "PT0D";

                                var nuevoActivo = new ActiveVideo
                                {
                                    VideoId = upc.Value.VideoId,
                                    Title = upc.Value.Title,
                                    ScheduledStartTime = upc.Value.ScheduledStartTime,
                                    ThumbnailUrl = upc.Value.ThumbnailUrl,
                                    AddedAt = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                                    IsPremiere = tieneDuracion // Se setea en base a la evaluación del estreno
                                };

                                await canalRef.Child("Actives").Child(upc.Key).PutAsync(nuevoActivo);
                                vivosSobrevivientes.Add(nuevoActivo);
                                await upcomingRef.DeleteAsync();
                                huboCambios = true;
                            }
                            else if (status == "none")
                            {
                                Console.WriteLine($"- {canal.Key}: El programado {upc.Key} fue cancelado o no existe. Borrando...");
                                await upcomingRef.DeleteAsync();
                            }
                            else if (status == "upcoming")
                            {
                                var horasAtrasado = (ahora - scheduledTime).TotalHours;
                                if (horasAtrasado > 24)
                                {
                                    Console.WriteLine($"- {canal.Key}: El programado {upc.Key} superó las 24hs. Eliminándolo...");
                                    await upcomingRef.DeleteAsync();
                                }
                            }
                        }
                    }
                }

                // --- C. RECALCULAR FALLBACK SI HUBIERON CAMBIOS ---
                if (huboCambios)
                {
                    if (vivosSobrevivientes.Any())
                    {
                        var streamGanador = vivosSobrevivientes
                            .OrderBy(v => v.IsPremiere)
                            .ThenByDescending(v => v.AddedAt ?? "")
                            .First();

                        Console.WriteLine($"> {canal.Key}: Recalculando... Portada global asignada a {streamGanador.VideoId}");

                        await canalRef.PatchAsync(new
                        {
                            ChannelLive = true,
                            LiveVideoId = streamGanador.VideoId,
                            ChannelImgLiveUrl = streamGanador.ThumbnailUrl ?? canal.Object.ChannelImgLiveUrl,
                            LastActivityAt = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                            IsPremiere = streamGanador.IsPremiere
                        });
                    }
                    else
                    {
                        Console.WriteLine($"> {canal.Key}: No quedaron streams vivos. APAGANDO CANAL.");
                        await canalRef.PatchAsync(new
                        {
                            ChannelLive = false,
                            LiveVideoId = "",
                            ChannelImgLiveUrl = "",
                            LastActivityAt = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                            IsPremiere = false
                        });
                    }
                }
            }

            Console.WriteLine("\n=== REVISIÓN UNIFICADA FINALIZADA ===");
        }
    }
}