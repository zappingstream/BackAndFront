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
    // ==========================================
    // MODELOS UNIFICADOS MONGODB
    // ==========================================
    #region Modelos
    [BsonIgnoreExtraElements]
    public class ZappingChannel
    {
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

        // Legacy
        public bool ChannelLive { get; set; }
        public string ChannelImgLiveUrl { get; set; }
        public string LiveVideoId { get; set; }
        public bool IsPremiere { get; set; }

        // Diccionarios
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
        public bool ToBeCut { get; set; }
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
        public bool ToBeCut { get; set; }
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
        public bool ToBeCut { get; set; }
        public string ScheduledStartTime { get; set; }
        public string ActualStartTime { get; set; }
        public string ActualEndTime { get; set; }
        public string EndedAt { get; set; }
    }
    #endregion

    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("ERROR: Especifica un comando: --livechecker, --removeRemovedPasts o --purgarDescartados");
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

            // 1. Configuración de dependencias
            var mongoClient = new MongoClient(mongoUri);
            var database = mongoClient.GetDatabase(dbName);
            var channelsCollection = database.GetCollection<ZappingChannel>("channels");

            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = ytApiKey,
                ApplicationName = "ZappingStreamSync"
            });

            // 2. Ruteo de comandos
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
                else if (args.Contains("--purgarDescartados"))
                {
                    await EjecutarPurgaDescartados(channelsCollection);
                }
                else
                {
                    Console.WriteLine("Comando no reconocido. Usa --livechecker, --removeRemovedPasts o --purgarDescartados");
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
                        videoIds.Add(upc.Key);
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

                // --- A. REVISIÓN DE VIVOS ---
                foreach (var kvp in vivosActuales.ToList())
                {
                    var ytVideo = infoDeYouTube.ContainsKey(kvp.Key) ? infoDeYouTube[kvp.Key] : null;

                    if (ytVideo == null)
                    {
                        if (!canal.Actives[kvp.Key].ToBeCut)
                        {
                            Console.WriteLine($"- {canal.Id}: Stream {kvp.Value.Title} fue borrado o es privado. Marcando como ToBeCut...");
                            canal.Actives[kvp.Key].ToBeCut = true;
                            huboCambios = true;
                        }
                    }
                    else
                    {
                        if (canal.Actives[kvp.Key].ToBeCut)
                        {
                            Console.WriteLine($"- {canal.Id}: El stream {kvp.Key} volvió a ser público. Restaurando (ToBeCut = false)...");
                            canal.Actives[kvp.Key].ToBeCut = false;
                            huboCambios = true;
                        }

                        if (ytVideo.Snippet?.LiveBroadcastContent != "live")
                        {
                            Console.WriteLine($"- {canal.Id}: Stream {kvp.Key} finalizó. Moviendo a Past...");

                            canal.Past[kvp.Key] = new PastVideo
                            {
                                VideoId = kvp.Key,
                                Title = ytVideo.Snippet?.Title ?? kvp.Value.Title,
                                ThumbnailUrl = kvp.Value.ThumbnailUrl,
                                WasPremiere = kvp.Value.IsPremiere,
                                PublishedAt = ytVideo.Snippet?.PublishedAtDateTimeOffset?.ToString("yyyy-MM-ddTHH:mm:ssZ") ?? kvp.Value.PublishedAt,
                                ScheduledStartTime = ytVideo.LiveStreamingDetails?.ScheduledStartTimeDateTimeOffset?.ToString("yyyy-MM-ddTHH:mm:ssZ") ?? kvp.Value.ScheduledStartTime,
                                ActualStartTime = ytVideo.LiveStreamingDetails?.ActualStartTimeDateTimeOffset?.ToString("yyyy-MM-ddTHH:mm:ssZ") ?? kvp.Value.ActualStartTime,
                                ActualEndTime = ytVideo.LiveStreamingDetails?.ActualEndTimeDateTimeOffset?.ToString("yyyy-MM-ddTHH:mm:ssZ") ?? sysTimeNow,
                                EndedAt = sysTimeNow
                            };

                            canal.Actives.Remove(kvp.Key);
                            huboCambios = true;
                        }
                        else
                        {
                            if (!kvp.Value.ToBeCut)
                            {
                                vivosSobrevivientes.Add(kvp.Value);
                            }
                        }
                    }
                }

                // --- B. REVISIÓN DE UPCOMING ---
                foreach (var upc in canal.Upcoming.ToList())
                {
                    var ytVideo = infoDeYouTube.ContainsKey(upc.Key) ? infoDeYouTube[upc.Key] : null;

                    if (ytVideo == null)
                    {
                        if (!canal.Upcoming[upc.Key].ToBeCut)
                        {
                            Console.WriteLine($"- {canal.Id}: El programado {upc.Key} fue cancelado/borrado. Marcando como ToBeCut...");
                            canal.Upcoming[upc.Key].ToBeCut = true;
                            huboCambios = true;
                        }
                    }
                    else
                    {
                        if (canal.Upcoming[upc.Key].ToBeCut)
                        {
                            Console.WriteLine($"- {canal.Id}: El programado {upc.Key} volvió a ser público. Restaurando (ToBeCut = false)...");
                            canal.Upcoming[upc.Key].ToBeCut = false;
                            huboCambios = true;
                        }

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
                        else if (status == "upcoming" && DateTimeOffset.TryParse(upc.Value.ScheduledStartTime, out var scheduledTime) && (ahora - scheduledTime).TotalHours > 24)
                        {
                            Console.WriteLine($"- {canal.Id}: El programado {upc.Key} superó las 24hs colgado. Eliminándolo definitivamente...");
                            canal.Upcoming.Remove(upc.Key);
                            huboCambios = true;
                        }
                    }
                }

                // --- C. RECALCULAR FALLBACK Y GUARDAR EN MONGO ---
                if (huboCambios)
                {
                    if (vivosSobrevivientes.Any())
                    {
                        var streamGanador = vivosSobrevivientes.OrderBy(v => v.IsPremiere).ThenByDescending(v => v.AddedAt ?? "").First();
                        Console.WriteLine($"> {canal.ChannelName}: Recalculando... Portada global asignada a {streamGanador.VideoId}");

                        canal.ChannelLive = true;
                        canal.LiveVideoId = streamGanador.VideoId;
                        canal.ChannelImgLiveUrl = streamGanador.ThumbnailUrl ?? canal.ChannelImgLiveUrl;
                        canal.LastActivityAt = sysTimeNow;
                        canal.IsPremiere = streamGanador.IsPremiere;
                    }
                    else
                    {
                        Console.WriteLine($"> {canal.ChannelName}: No quedaron streams vivos. APAGANDO CANAL.");

                        canal.ChannelLive = false;
                        canal.LiveVideoId = "";
                        canal.ChannelImgLiveUrl = "";
                        canal.LastActivityAt = sysTimeNow;
                        canal.IsPremiere = false;
                    }

                    await collection.ReplaceOneAsync(c => c.Id == canal.Id, canal);
                }
            }

            Console.WriteLine("\n=== LIVE CHECKER FINALIZADO ===");
        }

        // ==========================================
        // MÓDULO 2: MANTENIMIENTO DE PASTS
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
                    // Poda por tiempo físico (Offline, 0 cuota)
                    if (DateTimeOffset.TryParse(pastVideo.Value.EndedAt, out var fechaFinalizacion) && fechaFinalizacion < limite7Dias)
                    {
                        Console.WriteLine($"- {canal.ChannelName}: Eliminando {pastVideo.Key} (> 7 días).");
                        canal.Past.Remove(pastVideo.Key);
                        huboCambiosEnPasts = true;
                        continue;
                    }

                    // Si sobrevive la poda inicial, lo anotamos para actualizar su data
                    videoIdsParaConsultar.Add(pastVideo.Key);
                    pastsSobrevivientes[pastVideo.Key] = (canal, pastVideo.Value);
                }

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

            // 2. CONSULTAR A YOUTUBE
            var infoDeYouTube = await ConsultarVideosEnYouTube(yt, videoIdsParaConsultar);

            // 3. ACTUALIZAR METADATA O APLICAR BORRADO LÓGICO
            var canalesAActualizar = new HashSet<ZappingChannel>();

            foreach (var kvp in pastsSobrevivientes)
            {
                string videoId = kvp.Key;
                var canal = kvp.Value.Canal;
                var datosLocales = kvp.Value.Video;

                if (!infoDeYouTube.TryGetValue(videoId, out var ytVideo))
                {
                    if (!datosLocales.ToBeCut)
                    {
                        Console.WriteLine($"- {canal.ChannelName}: El VOD {videoId} ya no existe o es privado. Marcando como ToBeCut...");
                        canal.Past[videoId].ToBeCut = true;
                        canalesAActualizar.Add(canal);
                    }
                    continue;
                }

                if (datosLocales.ToBeCut)
                {
                    Console.WriteLine($"> {canal.ChannelName}: El VOD {videoId} volvió a ser público. Restaurando (ToBeCut = false)...");
                    canal.Past[videoId].ToBeCut = false;
                    canalesAActualizar.Add(canal);
                }

                string tituloFresco = ytVideo.Snippet?.Title ?? datosLocales.Title;

                if (tituloFresco != datosLocales.Title)
                {
                    Console.WriteLine($"> {canal.ChannelName}: Actualizando metadata del VOD {videoId}...");
                    canal.Past[videoId].Title = tituloFresco;
                    canalesAActualizar.Add(canal);
                }
            }

            foreach (var canal in canalesAActualizar)
            {
                await collection.ReplaceOneAsync(c => c.Id == canal.Id, canal);
            }

            Console.WriteLine("\n=== MANTENIMIENTO DE PASTS FINALIZADO ===");
        }

        // ==========================================
        // MÓDULO 3: PURGA DE DESCARTADOS (Soft Deletes > 24hs)
        // ==========================================
        static async Task EjecutarPurgaDescartados(IMongoCollection<ZappingChannel> collection)
        {
            Console.WriteLine("\n=== INICIANDO PURGA DE DESCARTADOS EN MONGODB ===");
            var canales = await collection.Find(_ => true).ToListAsync();

            var ahora = DateTimeOffset.UtcNow;
            string sysTimeNow = ahora.ToString("yyyy-MM-ddTHH:mm:ssZ");
            var limite24Horas = ahora.AddDays(-1);

            foreach (var canal in canales)
            {
                bool huboCambios = false;

                // 1. Limpiar Upcoming
                if (canal.Upcoming != null)
                {
                    foreach (var upc in canal.Upcoming.ToList())
                    {
                        string fechaRef = upc.Value.ScheduledStartTime ?? upc.Value.AddedAt ?? sysTimeNow;
                        if (upc.Value.ToBeCut && DateTimeOffset.TryParse(fechaRef, out var fecha) && fecha < limite24Horas)
                        {
                            Console.WriteLine($"- {canal.ChannelName}: Registro descartado en Upcoming purgado ({upc.Key}).");
                            canal.Upcoming.Remove(upc.Key);
                            huboCambios = true;
                        }
                    }
                }

                // 2. Limpiar Actives
                if (canal.Actives != null)
                {
                    foreach (var act in canal.Actives.ToList())
                    {
                        string fechaRef = act.Value.ActualStartTime ?? act.Value.AddedAt ?? sysTimeNow;
                        if (act.Value.ToBeCut && DateTimeOffset.TryParse(fechaRef, out var fecha) && fecha < limite24Horas)
                        {
                            Console.WriteLine($"- {canal.ChannelName}: Registro descartado en Actives purgado ({act.Key}).");
                            canal.Actives.Remove(act.Key);
                            huboCambios = true;
                        }
                    }
                }

                // 3. Limpiar Pasts
                if (canal.Past != null)
                {
                    foreach (var past in canal.Past.ToList())
                    {
                        string fechaRef = past.Value.EndedAt ?? sysTimeNow;
                        if (past.Value.ToBeCut && DateTimeOffset.TryParse(fechaRef, out var fecha) && fecha < limite24Horas)
                        {
                            Console.WriteLine($"- {canal.ChannelName}: Registro descartado en Past purgado ({past.Key}).");
                            canal.Past.Remove(past.Key);
                            huboCambios = true;
                        }
                    }
                }

                if (huboCambios)
                {
                    await collection.ReplaceOneAsync(c => c.Id == canal.Id, canal);
                }
            }

            Console.WriteLine("\n=== PURGA DE DESCARTADOS FINALIZADA ===");
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