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

        // Diccionarios - La única fuente de la verdad
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
                    await EjecutarLimpiezaPasts(channelsCollection);
                }
                else if (args.Contains("--purgarDescartados"))
                {
                    await EjecutarPurgaDescartados(channelsCollection, youtubeService);
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
                var vivosSobrevivientes = new List<ActiveVideo>();

                // --- A. REVISIÓN DE VIVOS ---
                foreach (var kvp in vivosActuales.ToList())
                {
                    var ytVideo = infoDeYouTube.ContainsKey(kvp.Key) ? infoDeYouTube[kvp.Key] : null;

                    if (ytVideo == null)
                    {
                        if (!canal.Actives[kvp.Key].ToBeCut)
                        {
                            Console.WriteLine($"- {canal.ChannelName}: Stream {kvp.Value.Title} fue borrado o es privado. Marcando como ToBeCut...");
                            canal.Actives[kvp.Key].ToBeCut = true;
                            huboCambios = true;
                        }
                    }
                    else
                    {
                        if (canal.Actives[kvp.Key].ToBeCut)
                        {
                            Console.WriteLine($"- {canal.ChannelName}: El stream {kvp.Key} volvió a ser público. Restaurando (ToBeCut = false)...");
                            canal.Actives[kvp.Key].ToBeCut = false;
                            huboCambios = true;
                        }

                        if (ytVideo.Snippet?.LiveBroadcastContent != "live")
                        {
                            Console.WriteLine($"- {canal.ChannelName}: Stream {kvp.Key} finalizó. Moviendo a Past...");

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
                            Console.WriteLine($"- {canal.ChannelName}: El programado {upc.Key} fue cancelado/borrado. Marcando como ToBeCut...");
                            canal.Upcoming[upc.Key].ToBeCut = true;
                            huboCambios = true;
                        }
                    }
                    else
                    {
                        if (canal.Upcoming[upc.Key].ToBeCut)
                        {
                            Console.WriteLine($"- {canal.ChannelName}: El programado {upc.Key} volvió a ser público. Restaurando (ToBeCut = false)...");
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
                            Console.WriteLine($"- {canal.ChannelName}: ¡El programado {upc.Key} está EN VIVO! Movido a Actives.");
                            bool tieneDuracion = ytVideo.ContentDetails != null && ytVideo.ContentDetails.Duration != "P0D" && ytVideo.ContentDetails.Duration != "PT0D";

                            var nuevoActivo = new ActiveVideo
                            {
                                VideoId = upc.Value.VideoId,
                                Title = ytVideo.Snippet?.Title ?? upc.Value.Title,
                                ThumbnailUrl = upc.Value.ThumbnailUrl,
                                // PARCHE 1: Rescatamos el booleano si ya venía como estreno
                                IsPremiere = upc.Value.IsPremiere || tieneDuracion,
                                PublishedAt = publishedAt ?? upc.Value.PublishedAt,
                                ScheduledStartTime = scheduledStart ?? upc.Value.ScheduledStartTime,
                                // PARCHE 2: Mantenemos las fechas originales
                                ActualStartTime = actualStart ?? upc.Value.ActualStartTime ?? sysTimeNow,
                                ActualEndTime = actualEnd,
                                AddedAt = upc.Value.AddedAt ?? sysTimeNow
                            };

                            canal.Actives[upc.Key] = nuevoActivo;
                            vivosSobrevivientes.Add(nuevoActivo);
                            canal.Upcoming.Remove(upc.Key);
                            huboCambios = true;
                        }
                        else if (status == "none")
                        {
                            Console.WriteLine($"- {canal.ChannelName}: El programado {upc.Key} es un video normal ahora. Moviendo a Past...");

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
                        else if (status == "upcoming")
                        {
                            // PARCHE 3: Interceptar cambios de horario silenciosos
                            if (!string.IsNullOrEmpty(scheduledStart) && scheduledStart != upc.Value.ScheduledStartTime)
                            {
                                Console.WriteLine($"- {canal.ChannelName}: Cambio de horario en {upc.Key}. De {upc.Value.ScheduledStartTime} a {scheduledStart}.");
                                canal.Upcoming[upc.Key].ScheduledStartTime = scheduledStart;
                                huboCambios = true;
                            }

                            // Control de limpieza de colgados (> 24hs)
                            if (DateTimeOffset.TryParse(canal.Upcoming[upc.Key].ScheduledStartTime, out var scheduledTime) && (ahora - scheduledTime).TotalHours > 24)
                            {
                                Console.WriteLine($"- {canal.ChannelName}: El programado {upc.Key} superó las 24hs colgado. Eliminándolo definitivamente...");
                                canal.Upcoming.Remove(upc.Key);
                                huboCambios = true;
                            }
                        }
                    }
                }

                // --- C. GUARDAR EN MONGO ---
                if (huboCambios)
                {
                    canal.LastActivityAt = sysTimeNow;

                    if (!vivosSobrevivientes.Any())
                    {
                        Console.WriteLine($"> {canal.ChannelName}: No quedaron streams vivos documentados.");
                    }

                    await collection.ReplaceOneAsync(c => c.Id == canal.Id, canal);
                }
            }

            Console.WriteLine("\n=== LIVE CHECKER FINALIZADO ===");
        }

        // ==========================================
        // MÓDULO 2: MANTENIMIENTO DE PASTS (Poda Física)
        // ==========================================
        static async Task EjecutarLimpiezaPasts(IMongoCollection<ZappingChannel> collection)
        {
            Console.WriteLine("\n=== INICIANDO PODA DE PASTS EN MONGODB ===");
            var canales = await collection.Find(_ => true).ToListAsync();

            // 1. Definimos nuestro offset de Argentina (UTC-3)
            var offsetArg = TimeSpan.FromHours(-3);

            // 2. Tomamos la hora actual, la pasamos a nuestra zona, y ahí recién sacamos la fecha
            var hoyArg = DateTimeOffset.UtcNow.ToOffset(offsetArg).Date;

            // 3. Restamos 7 días exactos. Si hoy es Lunes, esto es el Lunes pasado a las 00:00 locales.
            var limite7DiasArg = hoyArg.AddDays(-7);

            foreach (var canal in canales)
            {
                if (canal.Past == null || !canal.Past.Any()) continue;
                bool huboCambiosEnPasts = false;

                foreach (var pastVideo in canal.Past.ToList())
                {
                    string fechaRef = pastVideo.Value.ActualStartTime ?? pastVideo.Value.EndedAt;
                    if (DateTimeOffset.TryParse(fechaRef, out var fechaUtc))
                    {
                        // Convertimos la fecha del video a nuestra zona horaria local antes de comparar
                        var fechaVideoArg = fechaUtc.ToOffset(offsetArg).Date;

                        if (fechaVideoArg <= limite7DiasArg)
                        {
                            Console.WriteLine($"- {canal.ChannelName}: Eliminando {pastVideo.Key} (Terminó el {fechaVideoArg:yyyy-MM-dd} local, límite era {limite7DiasArg:yyyy-MM-dd}).");
                            canal.Past.Remove(pastVideo.Key);
                            huboCambiosEnPasts = true;
                        }
                    }
                }

                if (huboCambiosEnPasts)
                {
                    await collection.ReplaceOneAsync(c => c.Id == canal.Id, canal);
                }
            }

            Console.WriteLine("\n=== PODA DE PASTS FINALIZADA ===");
        }

        // ==========================================
        // MÓDULO 3: PURGA DE DESCARTADOS (Soft Deletes > 24hs) Y PASTS MUERTOS (> 12hs)
        // ==========================================
        static async Task EjecutarPurgaDescartados(IMongoCollection<ZappingChannel> collection, YouTubeService yt)
        {
            Console.WriteLine("\n=== INICIANDO PURGA DE DESCARTADOS EN MONGODB ===");
            var canales = await collection.Find(_ => true).ToListAsync();

            var ahora = DateTimeOffset.UtcNow;
            string sysTimeNow = ahora.ToString("yyyy-MM-ddTHH:mm:ssZ");

            var limite24Horas = ahora.AddDays(-1);
            var limite12Horas = ahora.AddHours(-12);

            // 1. RECOLECTAR IDs DE PAST QUE TENGAN MÁS DE 12 HORAS
            var videoIdsAVerificar = new HashSet<string>();
            foreach (var canal in canales)
            {
                if (canal.Past != null)
                {
                    foreach (var past in canal.Past)
                    {
                        string fechaRef = past.Value.EndedAt ?? sysTimeNow;
                        if (DateTimeOffset.TryParse(fechaRef, out var fecha))
                        {
                            // Si es un ToBeCut > 24hs, se borra seguro, no gastamos cuota de API en verificarlo
                            if (past.Value.ToBeCut && fecha < limite24Horas) continue;

                            // Si tiene más de 12 horas, lo anotamos para preguntar a YouTube
                            if (fecha < limite12Horas)
                            {
                                videoIdsAVerificar.Add(past.Key);
                            }
                        }
                    }
                }
            }

            // 2. CONSULTAR A YOUTUBE SU ESTADO ACTUAL
            var infoDeYouTube = await ConsultarVideosEnYouTube(yt, videoIdsAVerificar);

            // 3. EJECUTAR LIMPIEZA
            foreach (var canal in canales)
            {
                bool huboCambios = false;

                // --- Limpiar Upcoming ---
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

                // --- Limpiar Actives ---
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

                // --- Limpiar Pasts ---
                if (canal.Past != null)
                {
                    foreach (var past in canal.Past.ToList())
                    {
                        string fechaRef = past.Value.EndedAt ?? sysTimeNow;

                        if (DateTimeOffset.TryParse(fechaRef, out var fecha))
                        {
                            // Condición A: Es un Soft Delete y pasaron más de 24 horas (Eliminación forzada)
                            if (past.Value.ToBeCut && fecha < limite24Horas)
                            {
                                Console.WriteLine($"- {canal.ChannelName}: Registro descartado en Past purgado ({past.Key}).");
                                canal.Past.Remove(past.Key);
                                huboCambios = true;
                            }
                            // Condición B: Tiene más de 12 horas. Lo borramos SOLO SI ya no existe en YouTube.
                            else if (fecha < limite12Horas)
                            {
                                if (!infoDeYouTube.ContainsKey(past.Key))
                                {
                                    Console.WriteLine($"- {canal.ChannelName}: Registro Past ({past.Key}) eliminado. Superó 12hs y YA NO ESTÁ en YouTube.");
                                    canal.Past.Remove(past.Key);
                                    huboCambios = true;
                                }
                            }
                        }
                    }
                }

                // Guardar cambios si los hubo en este canal
                if (huboCambios)
                {
                    await collection.ReplaceOneAsync(c => c.Id == canal.Id, canal);
                }
            }

            Console.WriteLine("\n=== PURGA DE DESCARTADOS Y PASTS FINALIZADA ===");
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