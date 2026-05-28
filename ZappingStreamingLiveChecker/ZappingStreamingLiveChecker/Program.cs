using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Firebase.Database;
using Firebase.Database.Query;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;

namespace ZappingStreamSyncConsole
{
    public class UpcomingVideo
    {
        public string VideoId { get; set; }
        public string Title { get; set; }
        public string ScheduledStartTime { get; set; }
        public string ThumbnailUrl { get; set; }
        public string AddedAt { get; set; }
        public bool IsPremiere { get; set; }
    }

    public class ActiveVideo
    {
        public string VideoId { get; set; }
        public string Title { get; set; }
        public string ScheduledStartTime { get; set; }
        public string ThumbnailUrl { get; set; }
        public string AddedAt { get; set; }
        public bool IsPremiere { get; set; }
    }

    public class PastVideo
    {
        public string VideoId { get; set; }
        public string Title { get; set; }
        public string EndedAt { get; set; }
        public string ScheduledStartTime { get; set; }
        public string ThumbnailUrl { get; set; }
        public bool WasPremiere { get; set; }
    }

    public class FirebaseChannel
    {
        public string ChannelName { get; set; }
        public bool ChannelLive { get; set; }
        public string LiveVideoId { get; set; }
        public string ChannelImgLiveUrl { get; set; }
        public string LastActivityAt { get; set; }
        public bool IsPremiere { get; set; }

        public Dictionary<string, UpcomingVideo> Upcoming { get; set; }
        public Dictionary<string, ActiveVideo> Actives { get; set; }
        public Dictionary<string, PastVideo> Past { get; set; }
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
                ApplicationName = "ZappingStreamSync"
            });

            try
            {
                await SincronizarEstadoCanales(firebaseClient, youtubeService);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR CRÍTICO: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Environment.Exit(1);
            }
        }

        static async Task SincronizarEstadoCanales(FirebaseClient firebase, YouTubeService yt)
        {
            Console.WriteLine("\n=== INICIANDO SINCRONIZACIÓN UNIFICADA (VIVOS + UPCOMING + LIMPIEZA PAST) ===");
            var estadoActualFirebase = await firebase.Child("Channels").OnceAsync<FirebaseChannel>();

            var ahora = DateTimeOffset.UtcNow;
            var videoIds = new HashSet<string>();
            var limite12Horas = ahora.AddHours(-12);

            // 1. RECOLECTAR TODOS LOS IDs EN UNA SOLA BOLSA
            foreach (var canal in estadoActualFirebase)
            {
                if (canal.Object.Actives != null)
                {
                    foreach (var key in canal.Object.Actives.Keys) videoIds.Add(key);
                }
                else if (!string.IsNullOrEmpty(canal.Object.LiveVideoId))
                {
                    videoIds.Add(canal.Object.LiveVideoId);
                }

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

            // 2. CONSULTAR A YOUTUBE
            var infoDeYouTube = new Dictionary<string, Google.Apis.YouTube.v3.Data.Video>();
            if (videoIds.Any())
            {
                Console.WriteLine($"Se evaluarán {videoIds.Count} videos en YouTube...");
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
            }

            // 3. PROCESAR Y ACTUALIZAR CANAL POR CANAL
            foreach (var canal in estadoActualFirebase)
            {
                var canalRef = firebase.Child("Channels").Child(canal.Key);
                bool huboCambios = false;

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

                    if (ytVideo == null)
                    {
                        Console.WriteLine($"- {canal.Key}: Stream {kvp.Key} fue borrado o es privado. Eliminando directo de Actives...");
                        await canalRef.Child("Actives").Child(kvp.Key).DeleteAsync();
                        huboCambios = true;
                    }
                    else if (ytVideo.Snippet?.LiveBroadcastContent != "live")
                    {
                        Console.WriteLine($"- {canal.Key}: Stream {kvp.Key} finalizó. Moviendo a Past...");

                        // 👇 CORREGIDO: Se respeta el nulo si no hay fecha programada real
                        string startTimeFallbackVivos = !string.IsNullOrEmpty(kvp.Value.ScheduledStartTime)
                            ? kvp.Value.ScheduledStartTime
                            : ytVideo.LiveStreamingDetails?.ScheduledStartTimeDateTimeOffset?.ToString("yyyy-MM-ddTHH:mm:ssZ");

                        var pastData = new PastVideo
                        {
                            VideoId = kvp.Key,
                            Title = ytVideo.Snippet?.Title ?? kvp.Value.Title,
                            ScheduledStartTime = startTimeFallbackVivos,
                            EndedAt = ahora.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                            ThumbnailUrl = kvp.Value.ThumbnailUrl,
                            WasPremiere = kvp.Value.IsPremiere
                        };
                        await canalRef.Child("Past").Child(kvp.Key).PutAsync(pastData);
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
                            var upcomingRef = canalRef.Child("Upcoming").Child(upc.Key);

                            if (ytVideo == null)
                            {
                                Console.WriteLine($"- {canal.Key}: El programado {upc.Key} fue cancelado/borrado. Borrando directo...");
                                await upcomingRef.DeleteAsync();
                            }
                            else
                            {
                                string status = ytVideo.Snippet?.LiveBroadcastContent ?? "none";

                                if (status == "live")
                                {
                                    Console.WriteLine($"- {canal.Key}: ¡El programado {upc.Key} está EN VIVO! Movido a Actives.");
                                    bool tieneDuracion = ytVideo.ContentDetails != null && ytVideo.ContentDetails.Duration != "P0D" && ytVideo.ContentDetails.Duration != "PT0D";
                                    string fechaInicioYouTube = ytVideo.LiveStreamingDetails?.ActualStartTimeDateTimeOffset?.ToString("yyyy-MM-ddTHH:mm:ssZ") ?? ahora.ToString("yyyy-MM-ddTHH:mm:ssZ");

                                    var nuevoActivo = new ActiveVideo
                                    {
                                        VideoId = upc.Value.VideoId,
                                        Title = upc.Value.Title,
                                        // ScheduledStartTime se arrastra tal cual de Upcoming (que ya debe estar limpio)
                                        ScheduledStartTime = upc.Value.ScheduledStartTime,
                                        ThumbnailUrl = upc.Value.ThumbnailUrl,
                                        AddedAt = fechaInicioYouTube,
                                        IsPremiere = tieneDuracion
                                    };
                                    await canalRef.Child("Actives").Child(upc.Key).PutAsync(nuevoActivo);
                                    vivosSobrevivientes.Add(nuevoActivo);
                                    await upcomingRef.DeleteAsync();
                                    huboCambios = true;
                                }
                                else if (status == "none")
                                {
                                    Console.WriteLine($"- {canal.Key}: El programado {upc.Key} es un video normal ahora. Moviendo a Past...");

                                    // 👇 CORREGIDO: Se respeta el nulo al mover un cancelado a Past
                                    string startTimeFallbackUpcoming = !string.IsNullOrEmpty(upc.Value.ScheduledStartTime)
                                        ? upc.Value.ScheduledStartTime
                                        : ytVideo.LiveStreamingDetails?.ScheduledStartTimeDateTimeOffset?.ToString("yyyy-MM-ddTHH:mm:ssZ");

                                    var pastData = new PastVideo
                                    {
                                        VideoId = upc.Key,
                                        Title = ytVideo.Snippet?.Title ?? upc.Value.Title,
                                        ScheduledStartTime = startTimeFallbackUpcoming,
                                        EndedAt = ahora.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                                        ThumbnailUrl = upc.Value.ThumbnailUrl,
                                        WasPremiere = upc.Value.IsPremiere
                                    };
                                    await canalRef.Child("Past").Child(upc.Key).PutAsync(pastData);
                                    await upcomingRef.DeleteAsync();
                                }
                                else if (status == "upcoming" && (ahora - scheduledTime).TotalHours > 24)
                                {
                                    Console.WriteLine($"- {canal.Key}: El programado {upc.Key} superó las 24hs colgado. Eliminándolo definitivamente...");
                                    await upcomingRef.DeleteAsync();
                                }
                            }
                        }
                    }
                }

                // --- C. RECALCULAR FALLBACK SI HUBO CAMBIOS ---
                if (huboCambios)
                {
                    if (vivosSobrevivientes.Any())
                    {
                        var streamGanador = vivosSobrevivientes.OrderBy(v => v.IsPremiere).ThenByDescending(v => v.AddedAt ?? "").First();
                        Console.WriteLine($"> {canal.Key}: Recalculando... Portada global asignada a {streamGanador.VideoId}");
                        await canalRef.PatchAsync(new
                        {
                            ChannelLive = true,
                            LiveVideoId = streamGanador.VideoId,
                            ChannelImgLiveUrl = streamGanador.ThumbnailUrl ?? canal.Object.ChannelImgLiveUrl,
                            LastActivityAt = ahora.ToString("yyyy-MM-ddTHH:mm:ssZ"),
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
                            LastActivityAt = ahora.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                            IsPremiere = false
                        });
                    }
                }

                // --- D. LIMPIEZA DE PAST VIEJOS O INEXISTENTES ---
                if (canal.Object.Past != null)
                {
                    var limite7Dias = ahora.AddDays(-7);
                    foreach (var pastVideo in canal.Object.Past)
                    {
                        bool eliminar = false;
                        string razon = "";

                        if (DateTimeOffset.TryParse(pastVideo.Value.EndedAt, out var fechaFinalizacion))
                        {
                            if (fechaFinalizacion < limite7Dias)
                            {
                                eliminar = true;
                                razon = "Antigüedad > 7 días";
                            }
                            else if (fechaFinalizacion >= limite12Horas && !infoDeYouTube.ContainsKey(pastVideo.Key))
                            {
                                eliminar = true;
                                razon = "Borrado o Privado en YouTube post-transmisión";
                            }
                        }
                        else
                        {
                            eliminar = true;
                            razon = "Fecha EndedAt inválida";
                        }

                        if (eliminar)
                        {
                            Console.WriteLine($"- {canal.Key}: Removiendo de Past el video {pastVideo.Key}. Razón: {razon}");
                            await canalRef.Child("Past").Child(pastVideo.Key).DeleteAsync();
                        }
                    }
                }
            }

            Console.WriteLine("\n=== SINCRONIZACIÓN UNIFICADA FINALIZADA ===");
        }
    }
}