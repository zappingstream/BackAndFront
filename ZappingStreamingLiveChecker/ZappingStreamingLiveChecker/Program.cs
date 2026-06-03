using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ZappingStreamSyncConsole
{
    // --- MODELOS UNIFICADOS MONGODB ---
    [BsonIgnoreExtraElements]
    public class ZappingChannel
    {
        // Mantengo tu lógica: el nombre sanitizado sigue siendo el _id
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public string Id { get; set; }

        public string ChannelName { get; set; }
        public string ChannelDescription { get; set; }
        public string ChannelCity { get; set; }
        public string ChannelType { get; set; }
        public string ChannelLiveUrl { get; set; }
        public string ChannelImgUrl { get; set; }
        public string ChannelBannerUrl { get; set; }
        public string LastActivityAt { get; set; }

        // --- LEGACY ---
        public bool ChannelLive { get; set; }
        public string ChannelImgLiveUrl { get; set; }
        public string LiveVideoId { get; set; }
        public bool IsPremiere { get; set; }

        // --- COLECCIONES ---
        public Dictionary<string, UpcomingVideo> Upcoming { get; set; }
        public Dictionary<string, ActiveVideo> Actives { get; set; }
        public Dictionary<string, PastVideo> Past { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class UpcomingVideo
    {
        public string VideoId { get; set; }
        public string Title { get; set; }
        public string ThumbnailUrl { get; set; }
        public bool IsPremiere { get; set; }
        public string PublishedAt { get; set; }
        public string ScheduledStartTime { get; set; }
        public string ActualStartTime { get; set; }
        public string ActualEndTime { get; set; }
        public string AddedAt { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class ActiveVideo
    {
        public string VideoId { get; set; }
        public string Title { get; set; }
        public string ThumbnailUrl { get; set; }
        public bool IsPremiere { get; set; }
        public string PublishedAt { get; set; }
        public string ScheduledStartTime { get; set; }
        public string ActualStartTime { get; set; }
        public string ActualEndTime { get; set; }
        public string AddedAt { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class PastVideo
    {
        public string VideoId { get; set; }
        public string Title { get; set; }
        public string ThumbnailUrl { get; set; }
        public bool WasPremiere { get; set; }
        public string PublishedAt { get; set; }
        public string ScheduledStartTime { get; set; }
        public string ActualStartTime { get; set; }
        public string ActualEndTime { get; set; }
        public string EndedAt { get; set; }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Por favor, especifica un comando: --livechecker o --removeRemovedPasts");
                Environment.Exit(1);
            }

            string ytApiKey = Environment.GetEnvironmentVariable("YOUTUBE_APIKEY") ?? "";
            string mongoUri = Environment.GetEnvironmentVariable("MONGODB_CONNECTIONSTRING") ?? "";
            string dbName = Environment.GetEnvironmentVariable("MONGODB_DATABASENAME") ?? "ZappingStreaming";

            if (string.IsNullOrEmpty(ytApiKey) || string.IsNullOrEmpty(mongoUri))
            {
                Console.WriteLine("ERROR FATAL: Faltan las variables de entorno (YOUTUBE_APIKEY o MONGO_URI).");
                Environment.Exit(1);
            }

            // 1. Configurar MongoDB
            var mongoClient = new MongoClient(mongoUri);
            var database = mongoClient.GetDatabase(dbName);
            var channelsCollection = database.GetCollection<ZappingChannel>("channels");

            // 2. Configurar YouTube
            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = ytApiKey,
                ApplicationName = "ZappingStreamSync"
            });

            try
            {
                if (args.Contains("--livechecker"))
                {
                    await EjecutarLiveChecker(channelsCollection, youtubeService);
                }
                else if (args.Contains("--removeRemovedPasts"))
                {
                    await EjecutarLimpiezaPasts(channelsCollection, youtubeService);
                }
                else
                {
                    Console.WriteLine("Comando no reconocido. Usa --livechecker o --removeRemovedPasts");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR CRÍTICO: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Environment.Exit(1);
            }
        }

        // ==========================================
        // MÓDULO 1: LIVE CHECKER (Vivos y Upcoming)
        // ==========================================
        static async Task EjecutarLiveChecker(IMongoCollection<ZappingChannel> collection, YouTubeService yt)
        {
            Console.WriteLine("\n=== INICIANDO LIVE CHECKER EN MONGODB ===");
            var canales = await collection.Find(_ => true).ToListAsync();

            var ahora = DateTimeOffset.UtcNow;
            string sysTimeNow = ahora.ToString("yyyy-MM-ddTHH:mm:ssZ");
            var videoIds = new HashSet<string>();

            // 1. RECOLECTAR IDs DE ACTIVES Y UPCOMING
            foreach (var canal in canales)
            {
                if (canal.Actives != null)
                {
                    foreach (var key in canal.Actives.Keys) videoIds.Add(key);
                }
                else if (!string.IsNullOrEmpty(canal.LiveVideoId))
                {
                    videoIds.Add(canal.LiveVideoId);
                }

                if (canal.Upcoming != null)
                {
                    foreach (var upc in canal.Upcoming)
                    {
                        if (DateTimeOffset.TryParse(upc.Value.ScheduledStartTime, out var scheduledTime) && scheduledTime <= ahora)
                        {
                            videoIds.Add(upc.Key);
                        }
                    }
                }
            }

            // 2. CONSULTAR A YOUTUBE
            var infoDeYouTube = await ConsultarVideosEnYouTube(yt, videoIds);

            // 3. PROCESAR
            foreach (var canal in canales)
            {
                bool huboCambios = false;

                canal.Upcoming ??= new Dictionary<string, UpcomingVideo>();
                canal.Actives ??= new Dictionary<string, ActiveVideo>();
                canal.Past ??= new Dictionary<string, PastVideo>();

                var vivosActuales = canal.Actives;
                if (!vivosActuales.Any() && !string.IsNullOrEmpty(canal.LiveVideoId))
                {
                    vivosActuales[canal.LiveVideoId] = new ActiveVideo { VideoId = canal.LiveVideoId };
                }

                var vivosSobrevivientes = new List<ActiveVideo>();

                // --- A. LIMPIEZA DE VIVOS ---
                foreach (var kvp in vivosActuales.ToList())
                {
                    var ytVideo = infoDeYouTube.ContainsKey(kvp.Key) ? infoDeYouTube[kvp.Key] : null;

                    if (ytVideo == null)
                    {
                        Console.WriteLine($"- {canal.Id}: Stream {kvp.Key} fue borrado o es privado. Eliminando directo de Actives...");
                        canal.Actives.Remove(kvp.Key);
                        huboCambios = true;
                    }
                    else if (ytVideo.Snippet?.LiveBroadcastContent != "live")
                    {
                        Console.WriteLine($"- {canal.Id}: Stream {kvp.Key} finalizó. Moviendo a Past...");

                        string publishedAt = ytVideo.Snippet?.PublishedAtDateTimeOffset?.ToString("yyyy-MM-ddTHH:mm:ssZ");
                        string scheduledStart = ytVideo.LiveStreamingDetails?.ScheduledStartTimeDateTimeOffset?.ToString("yyyy-MM-ddTHH:mm:ssZ");
                        string actualStart = ytVideo.LiveStreamingDetails?.ActualStartTimeDateTimeOffset?.ToString("yyyy-MM-ddTHH:mm:ssZ");
                        string actualEnd = ytVideo.LiveStreamingDetails?.ActualEndTimeDateTimeOffset?.ToString("yyyy-MM-ddTHH:mm:ssZ");

                        canal.Past[kvp.Key] = new PastVideo
                        {
                            VideoId = kvp.Key,
                            Title = ytVideo.Snippet?.Title ?? kvp.Value.Title,
                            ThumbnailUrl = kvp.Value.ThumbnailUrl,
                            WasPremiere = kvp.Value.IsPremiere,
                            PublishedAt = publishedAt ?? kvp.Value.PublishedAt,
                            ScheduledStartTime = scheduledStart ?? kvp.Value.ScheduledStartTime,
                            ActualStartTime = actualStart ?? kvp.Value.ActualStartTime,
                            ActualEndTime = actualEnd ?? sysTimeNow,
                            EndedAt = sysTimeNow
                        };

                        canal.Actives.Remove(kvp.Key);
                        huboCambios = true;
                    }
                    else
                    {
                        vivosSobrevivientes.Add(kvp.Value);
                    }
                }

                // --- B. REVISIÓN DE UPCOMING ---
                foreach (var upc in canal.Upcoming.ToList())
                {
                    if (DateTimeOffset.TryParse(upc.Value.ScheduledStartTime, out var scheduledTime) && scheduledTime <= ahora)
                    {
                        var ytVideo = infoDeYouTube.ContainsKey(upc.Key) ? infoDeYouTube[upc.Key] : null;

                        if (ytVideo == null)
                        {
                            Console.WriteLine($"- {canal.Id}: El programado {upc.Key} fue cancelado/borrado. Borrando directo...");
                            canal.Upcoming.Remove(upc.Key);
                            huboCambios = true;
                        }
                        else
                        {
                            string status = ytVideo.Snippet?.LiveBroadcastContent ?? "none";
                            string publishedAt = ytVideo.Snippet?.PublishedAtDateTimeOffset?.ToString("yyyy-MM-ddTHH:mm:ssZ");
                            string scheduledStart = ytVideo.LiveStreamingDetails?.ScheduledStartTimeDateTimeOffset?.ToString("yyyy-MM-ddTHH:mm:ssZ");
                            string actualStart = ytVideo.LiveStreamingDetails?.ActualStartTimeDateTimeOffset?.ToString("yyyy-MM-ddTHH:mm:ssZ");
                            string actualEnd = ytVideo.LiveStreamingDetails?.ActualEndTimeDateTimeOffset?.ToString("yyyy-MM-ddTHH:mm:ssZ");

                            if (status == "live")
                            {
                                Console.WriteLine($"- {canal.Id}: ¡El programado {upc.Key} está EN VIVO! Movido a Actives.");
                                bool tieneDuracion = ytVideo.ContentDetails != null && ytVideo.ContentDetails.Duration != "P0D" && ytVideo.ContentDetails.Duration != "PT0D";

                                var nuevoActivo = new ActiveVideo
                                {
                                    VideoId = upc.Value.VideoId,
                                    Title = ytVideo.Snippet?.Title ?? upc.Value.Title,
                                    ThumbnailUrl = upc.Value.ThumbnailUrl,
                                    IsPremiere = tieneDuracion,
                                    PublishedAt = publishedAt ?? upc.Value.PublishedAt,
                                    ScheduledStartTime = scheduledStart ?? upc.Value.ScheduledStartTime,
                                    ActualStartTime = actualStart ?? sysTimeNow,
                                    ActualEndTime = actualEnd,
                                    AddedAt = sysTimeNow
                                };

                                canal.Actives[upc.Key] = nuevoActivo;
                                vivosSobrevivientes.Add(nuevoActivo);
                                canal.Upcoming.Remove(upc.Key);
                                huboCambios = true;
                            }
                            else if (status == "none")
                            {
                                Console.WriteLine($"- {canal.Id}: El programado {upc.Key} es un video normal ahora. Moviendo a Past...");

                                canal.Past[upc.Key] = new PastVideo
                                {
                                    VideoId = upc.Key,
                                    Title = ytVideo.Snippet?.Title ?? upc.Value.Title,
                                    ThumbnailUrl = upc.Value.ThumbnailUrl,
                                    WasPremiere = upc.Value.IsPremiere,
                                    PublishedAt = publishedAt ?? upc.Value.PublishedAt,
                                    ScheduledStartTime = scheduledStart ?? upc.Value.ScheduledStartTime,
                                    ActualStartTime = actualStart ?? upc.Value.ActualStartTime,
                                    ActualEndTime = actualEnd ?? sysTimeNow,
                                    EndedAt = sysTimeNow
                                };

                                canal.Upcoming.Remove(upc.Key);
                                huboCambios = true;
                            }
                            else if (status == "upcoming" && (ahora - scheduledTime).TotalHours > 24)
                            {
                                Console.WriteLine($"- {canal.Id}: El programado {upc.Key} superó las 24hs colgado. Eliminándolo definitivamente...");
                                canal.Upcoming.Remove(upc.Key);
                                huboCambios = true;
                            }
                        }
                    }
                }

                // --- C. RECALCULAR FALLBACK Y GUARDAR EN MONGO ---
                if (huboCambios)
                {
                    if (vivosSobrevivientes.Any())
                    {
                        var streamGanador = vivosSobrevivientes.OrderBy(v => v.IsPremiere).ThenByDescending(v => v.AddedAt ?? "").First();
                        Console.WriteLine($"> {canal.Id}: Recalculando... Portada global asignada a {streamGanador.VideoId}");

                        canal.ChannelLive = true;
                        canal.LiveVideoId = streamGanador.VideoId;
                        canal.ChannelImgLiveUrl = streamGanador.ThumbnailUrl ?? canal.ChannelImgLiveUrl;
                        canal.LastActivityAt = sysTimeNow;
                        canal.IsPremiere = streamGanador.IsPremiere;
                    }
                    else
                    {
                        Console.WriteLine($"> {canal.Id}: No quedaron streams vivos. APAGANDO CANAL.");

                        canal.ChannelLive = false;
                        canal.LiveVideoId = "";
                        canal.ChannelImgLiveUrl = "";
                        canal.LastActivityAt = sysTimeNow;
                        canal.IsPremiere = false;
                    }

                    // En vez de multiples HTTP requests a Firebase, pisamos todo atómicamente
                    await collection.ReplaceOneAsync(c => c.Id == canal.Id, canal);
                }
            }

            Console.WriteLine("\n=== LIVE CHECKER FINALIZADO ===");
        }

        // ==========================================
        // MÓDULO 2: MANTENIMIENTO DE PASTS (Limpieza y Actualización de Metadata)
        // ==========================================
        static async Task EjecutarLimpiezaPasts(IMongoCollection<ZappingChannel> collection, YouTubeService yt)
        {
            Console.WriteLine("\n=== INICIANDO MANTENIMIENTO DE PASTS EN MONGODB ===");
            var canales = await collection.Find(_ => true).ToListAsync();

            var ahora = DateTimeOffset.UtcNow;
            var limite7Dias = ahora.AddDays(-7);

            var videoIdsParaConsultar = new HashSet<string>();
            var pastsSobrevivientes = new Dictionary<string, (ZappingChannel Canal, PastVideo Video)>();

            // 1. PODA OFFLINE Y RECOLECCIÓN
            foreach (var canal in canales)
            {
                if (canal.Past == null || !canal.Past.Any()) continue;
                bool huboCambiosEnPasts = false;

                foreach (var pastVideo in canal.Past.ToList())
                {
                    // Poda por tiempo (Offline, 0 cuota)
                    if (DateTimeOffset.TryParse(pastVideo.Value.EndedAt, out var fechaFinalizacion) && fechaFinalizacion < limite7Dias)
                    {
                        Console.WriteLine($"- {canal.Id}: Eliminando {pastVideo.Key} (> 7 días).");
                        canal.Past.Remove(pastVideo.Key);
                        huboCambiosEnPasts = true;
                        continue;
                    }

                    // Si sobrevive, lo anotamos para ir a buscar su data fresca a YouTube
                    videoIdsParaConsultar.Add(pastVideo.Key);
                    pastsSobrevivientes[pastVideo.Key] = (canal, pastVideo.Value);
                }

                // Si borramos videos viejos, hacemos un update rápido en Mongo
                if (huboCambiosEnPasts)
                {
                    await collection.ReplaceOneAsync(c => c.Id == canal.Id, canal);
                }
            }

            if (!videoIdsParaConsultar.Any())
            {
                Console.WriteLine("No hay videos recientes en Past para actualizar. Saliendo...");
                return;
            }

            // 2. CONSULTAR A YOUTUBE (Cuesta 1 punto por cada lote de 50)
            var infoDeYouTube = await ConsultarVideosEnYouTube(yt, videoIdsParaConsultar);

            // 3. ACTUALIZAR O BORRAR
            var canalesAActualizar = new HashSet<ZappingChannel>();

            foreach (var kvp in pastsSobrevivientes)
            {
                string videoId = kvp.Key;
                var canal = kvp.Value.Canal;
                var datosLocales = kvp.Value.Video;

                if (!infoDeYouTube.TryGetValue(videoId, out var ytVideo))
                {
                    Console.WriteLine($"- {canal.Id}: El VOD {videoId} ya no existe o es privado. Borrando de MongoDB...");
                    canal.Past.Remove(videoId);
                    canalesAActualizar.Add(canal);
                    continue;
                }

                // Extraer metadata fresca
                string tituloFresco = ytVideo.Snippet?.Title ?? datosLocales.Title;

                if (tituloFresco != datosLocales.Title)
                {
                    Console.WriteLine($"> {canal.Id}: Actualizando metadata del VOD {videoId}...");
                    canal.Past[videoId].Title = tituloFresco;
                    canalesAActualizar.Add(canal);
                }
            }

            // Guardar cambios finales
            foreach (var canal in canalesAActualizar)
            {
                await collection.ReplaceOneAsync(c => c.Id == canal.Id, canal);
            }

            Console.WriteLine("\n=== MANTENIMIENTO DE PASTS FINALIZADO ===");
        }

        // ==========================================
        // MÉTODO AUXILIAR PARA LLAMAR A YOUTUBE
        // ==========================================
        static async Task<Dictionary<string, Google.Apis.YouTube.v3.Data.Video>> ConsultarVideosEnYouTube(YouTubeService yt, HashSet<string> videoIds)
        {
            var infoDeYouTube = new Dictionary<string, Google.Apis.YouTube.v3.Data.Video>();
            if (videoIds.Any())
            {
                Console.WriteLine($"Consultando {videoIds.Count} videos en YouTube...");
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
            return infoDeYouTube;
        }
    }
}
