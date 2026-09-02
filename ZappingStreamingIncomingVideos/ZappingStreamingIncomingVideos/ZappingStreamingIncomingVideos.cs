using Firebase.Database;
using Firebase.Database.Query;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ZappingStreamingIncomingVideos
{
    [BsonIgnoreExtraElements]
    public class ChannelOriginItem
    {
        // En Mongo, el ID de YouTube ("UC...") será nuestra clave primaria _id
        [MongoDB.Bson.Serialization.Attributes.BsonId]
        [BsonRepresentation(BsonType.String)]
        [JsonPropertyName("ChannelId")]
        public string ChannelId { get; set; }

        [BsonElement("title")]
        public string Title { get; set; }

        [BsonElement("city")]
        public string City { get; set; }

        [BsonElement("category")]
        public string Category { get; set; }
    }
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


        // --- COLECCIONES ---
        public Dictionary<string, UpcomingVideo> Upcoming { get; set; }
        public Dictionary<string, ActiveVideo> Actives { get; set; }
        public Dictionary<string, PastVideo> Past { get; set; }
        public Dictionary<string, DiscardedVideo> Discarded { get; set; }
    }

    public class DiscardedVideo
    {
        public string VideoId { get; set; }
        public string PublishedAt { get; set; }
    }

    public class PastVideo
    {
        public string VideoId { get; set; }
        public string Title { get; set; }
        public string ThumbnailUrl { get; set; }
        public bool WasPremiere { get; set; }

        // Tiempos estandarizados
        public string PublishedAt { get; set; }
        public string ScheduledStartTime { get; set; }
        public string ActualStartTime { get; set; }
        public string ActualEndTime { get; set; }
        public string EndedAt { get; set; }
        public bool ToBeCut { get; set; } // <--- AGREGADO
    }

    public class UpcomingVideo
    {
        public string VideoId { get; set; }
        public string Title { get; set; }
        public string ThumbnailUrl { get; set; }
        public bool IsPremiere { get; set; }

        // Tiempos estandarizados
        public string PublishedAt { get; set; }
        public string ScheduledStartTime { get; set; }
        public string ActualStartTime { get; set; }
        public string ActualEndTime { get; set; }
        public string AddedAt { get; set; }
        public bool ToBeCut { get; set; } // <--- AGREGADO
    }

    public class ActiveVideo
    {
        public string VideoId { get; set; }
        public string Title { get; set; }
        public string ThumbnailUrl { get; set; }
        public bool IsPremiere { get; set; }

        // Tiempos estandarizados
        public string PublishedAt { get; set; }
        public string ScheduledStartTime { get; set; }
        public string ActualStartTime { get; set; }
        public string ActualEndTime { get; set; }
        public string AddedAt { get; set; }
        public bool ToBeCut { get; set; } // <--- AGREGADO
    }

    public class ZappingStreamingIncomingVideos
    {
        private readonly HttpClient _httpClient;
        private readonly IMongoDatabase _database;
        private readonly YouTubeService _youtubeService;
        private readonly ILogger<ZappingStreamingIncomingVideos> _logger;
        private readonly IMongoCollection<ZappingChannel> _channelsCollection;

        public ZappingStreamingIncomingVideos(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<ZappingStreamingIncomingVideos> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            // Configuración de MongoDB
            string mongoUri = configuration["MongoDB:ConnectionString"];
            string dbName = configuration["MongoDB:DatabaseName"] ?? "ZappingStreaming";
            var mongoClient = new MongoClient(mongoUri);
            _database = mongoClient.GetDatabase(dbName);
            _channelsCollection = _database.GetCollection<ZappingChannel>("channels");

            // Configuración de YouTube
            string ytApiKey = configuration["YouTube:ApiKey"] ?? "";
            _youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = ytApiKey,
                ApplicationName = "ZappingStreamingWorker"
            });
        }

        public async Task ExecuteAsync(CancellationToken stoppingToken = default)
        {
            try
            {
                _logger.LogInformation("=== INICIANDO EXTRACCIÓN DE VIDEOS RSS ===");
                await ProcesarVideosDesdeRSSAsync(stoppingToken);
                _logger.LogInformation("=== EXTRACCIÓN DE VIDEOS COMPLETADA CON ÉXITO ===");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocurrió un error crítico durante la ejecución.");
            }
            finally
            {
                _logger.LogInformation("Proceso finalizado.");
            }
        }

        private async Task ProcesarVideosDesdeRSSAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Obteniendo lista de canales desde MongoDB...");

            // Obtenemos los canales de la colección 'channels'
            var canales = await _channelsCollection.Find(_ => true).ToListAsync(cancellationToken);

            if (!canales.Any())
            {
                _logger.LogWarning("La colección 'channels' está vacía. No hay canales para procesar.");
                return;
            }

            var todosLosNuevosVideos = new List<(string VideoId, string ChannelId)>();

            foreach (var canal in canales)
            {
                if (cancellationToken.IsCancellationRequested) break;

                string channelId = canal.Id;
                if (string.IsNullOrEmpty(channelId) || !channelId.StartsWith("UC")) continue;

                _logger.LogInformation("Procesando RSS del canal: {ChannelName} ({ChannelId})", canal.ChannelName, channelId);

                var nuevosVideos = new List<string>();
                bool rssfalla = false;

                try
                {
                    var response = await _httpClient.GetAsync($"https://www.youtube.com/feeds/videos.xml?channel_id={channelId}", cancellationToken);
                    if (!response.IsSuccessStatusCode)
                    {
                        rssfalla = true;
                    }
                    else
                    {
                        var feedXml = await response.Content.ReadAsStringAsync(cancellationToken);
                        var feedDoc = XDocument.Parse(feedXml);
                        XNamespace atom = "http://www.w3.org/2005/Atom";
                        XNamespace yt = "http://www.youtube.com/xml/schemas/2015";

                        var entries = feedDoc.Descendants(atom + "entry");

                        foreach (var entry in entries)
                        {
                            var publishedStr = entry.Element(atom + "published")?.Value;
                            if (DateTime.TryParse(publishedStr, out var pubDate) && pubDate.ToUniversalTime() < DateTime.UtcNow.Date.AddHours(-5))
                            {
                                continue; // Filtramos videos anteriores a ayer a las 19:00
                            }

                            var linkElement = entry.Elements(atom + "link").FirstOrDefault(l => l.Attribute("rel")?.Value == "alternate");
                            string linkHref = linkElement?.Attribute("href")?.Value ?? "";

                            if (linkHref.Contains("/shorts/")) continue;

                            var videoId = entry.Element(yt + "videoId")?.Value;
                            if (!string.IsNullOrEmpty(videoId))
                            {
                                bool yaExiste = (canal.Actives != null && canal.Actives.ContainsKey(videoId)) ||
                                                (canal.Upcoming != null && canal.Upcoming.ContainsKey(videoId)) ||
                                                (canal.Past != null && canal.Past.ContainsKey(videoId)) ||
                                                (canal.Discarded != null && canal.Discarded.ContainsKey(videoId));
                                if (!yaExiste) nuevosVideos.Add(videoId);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    rssfalla = true;
                }

                if (rssfalla)
                {
                    _logger.LogWarning("Falló lectura XML directa de {ChannelId}, intentando con alternativa JSON (rss2json)...", channelId);
                    try
                    {
                        var jsonResponse = await _httpClient.GetAsync($"https://api.rss2json.com/v1/api.json?rss_url=https://www.youtube.com/feeds/videos.xml?channel_id={channelId}", cancellationToken);
                        if (jsonResponse.IsSuccessStatusCode)
                        {
                            var jsonString = await jsonResponse.Content.ReadAsStringAsync(cancellationToken);
                            using var jsonDoc = JsonDocument.Parse(jsonString);

                            if (jsonDoc.RootElement.TryGetProperty("items", out var items))
                            {
                                foreach (var item in items.EnumerateArray())
                                {
                                    string pubDateStr = item.TryGetProperty("pubDate", out var pdProp) ? pdProp.GetString() : null;
                                    if (DateTime.TryParse(pubDateStr, out var pubDate) && pubDate.ToUniversalTime() < DateTime.UtcNow.Date.AddHours(-5))
                                    {
                                        continue; // Filtramos videos anteriores a ayer a las 19:00
                                    }

                                    string linkHref = item.TryGetProperty("link", out var linkProp) ? linkProp.GetString() ?? "" : "";
                                    if (linkHref.Contains("/shorts/")) continue;

                                    string videoId = "";
                                    if (item.TryGetProperty("guid", out var guidProp))
                                    {
                                        string guid = guidProp.GetString() ?? "";
                                        if (guid.StartsWith("yt:video:"))
                                        {
                                            videoId = guid.Replace("yt:video:", "");
                                        }
                                    }

                                    if (string.IsNullOrEmpty(videoId))
                                    {
                                        var match = Regex.Match(linkHref, @"v=([^&]+)");
                                        if (match.Success) videoId = match.Groups[1].Value;
                                    }

                                    if (!string.IsNullOrEmpty(videoId))
                                    {
                                        bool yaExiste = (canal.Actives != null && canal.Actives.ContainsKey(videoId)) ||
                                                        (canal.Upcoming != null && canal.Upcoming.ContainsKey(videoId)) ||
                                                        (canal.Past != null && canal.Past.ContainsKey(videoId)) ||
                                                        (canal.Discarded != null && canal.Discarded.ContainsKey(videoId));
                                        if (!yaExiste) nuevosVideos.Add(videoId);
                                    }
                                }
                            }
                        }
                        else
                        {
                            _logger.LogError("La alternativa JSON también falló para {ChannelId} (Status: {StatusCode}).", channelId, jsonResponse.StatusCode);
                        }
                    }
                    catch (Exception jsonEx)
                    {
                        _logger.LogError(jsonEx, "Error al procesar la alternativa JSON de {ChannelId}.", channelId);
                    }
                }

                if (nuevosVideos.Any())
                {
                    _logger.LogInformation("Se encontraron {Count} videos nuevos en {ChannelName}.", nuevosVideos.Count, canal.ChannelName);
                    foreach (var vid in nuevosVideos)
                    {
                        todosLosNuevosVideos.Add((vid, channelId));
                    }
                }
                else
                {
                    _logger.LogInformation("No se encontraron videos nuevos en {ChannelName}.", canal.ChannelName);
                }

                await Task.Delay(500, cancellationToken); // Pequeño delay para no saturar
            }

            if (todosLosNuevosVideos.Any())
            {
                _logger.LogInformation("Se encontraron un total de {Count} videos nuevos en todos los canales. Consultando API de YouTube en lotes...", todosLosNuevosVideos.Count);
                await ProcesarBatchGlobalAsync(todosLosNuevosVideos, cancellationToken);
            }
            else
            {
                _logger.LogInformation("No se encontraron videos nuevos en ningún canal. Tarea finalizada.");
            }
        }

        private async Task ProcesarBatchGlobalAsync(List<(string VideoId, string ChannelId)> videosAProcesar, CancellationToken cancellationToken)
        {
            try
            {
                // La API de YouTube acepta hasta 50 IDs por request
                var lotes = videosAProcesar.Chunk(50);

                foreach (var lote in lotes)
                {
                    string idsJuntos = string.Join(",", lote.Select(v => v.VideoId));
                    var videoRequest = _youtubeService.Videos.List("snippet,contentDetails,liveStreamingDetails");
                    videoRequest.Id = idsJuntos;
                    var videoResponse = await videoRequest.ExecuteAsync(cancellationToken);

                    var videosEncontrados = videoResponse.Items ?? new List<Google.Apis.YouTube.v3.Data.Video>();

                    foreach (var videoItem in lote)
                    {
                        try
                        {
                            var videoInfo = videosEncontrados.FirstOrDefault(v => v.Id == videoItem.VideoId);
                            if (videoInfo != null)
                            {
                                await ActualizarMongoParaVideoAsync(videoItem.VideoId, videoItem.ChannelId, videoInfo);
                            }
                            else
                            {
                                _logger.LogWarning("No se encontraron detalles en la API de YouTube para el video {VideoId} (Canal {ChannelId}).", videoItem.VideoId, videoItem.ChannelId);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error procesando el video {VideoId} del canal {ChannelId}.", videoItem.VideoId, videoItem.ChannelId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error grave procesando el batch global de YouTube.");
            }
        }

        private async Task ActualizarMongoParaVideoAsync(string videoId, string channelIdInfo, Google.Apis.YouTube.v3.Data.Video videoInfo)
        {
            string sysTimeNow = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

            // 1. EXTRACCIÓN DE DATOS DE YOUTUBE
            string publishedAt = videoInfo?.Snippet?.PublishedAtDateTimeOffset?.ToString("yyyy-MM-ddTHH:mm:ssZ");
            string scheduledStart = videoInfo?.LiveStreamingDetails?.ScheduledStartTimeDateTimeOffset?.ToString("yyyy-MM-ddTHH:mm:ssZ");
            string actualStart = videoInfo?.LiveStreamingDetails?.ActualStartTimeDateTimeOffset?.ToString("yyyy-MM-ddTHH:mm:ssZ");
            string actualEnd = videoInfo?.LiveStreamingDetails?.ActualEndTimeDateTimeOffset?.ToString("yyyy-MM-ddTHH:mm:ssZ");

            string broadcastStatus = videoInfo?.Snippet?.LiveBroadcastContent ?? "none";
            bool esEnVivo = broadcastStatus == "live";
            bool esUpcoming = broadcastStatus == "upcoming";
            bool tieneDuracion = videoInfo?.ContentDetails != null &&
                                 videoInfo.ContentDetails.Duration != "P0D" &&
                                 videoInfo.ContentDetails.Duration != "PT0D";
            bool esEstreno = (esEnVivo || esUpcoming) && tieneDuracion;
            string liveImageUrl = videoInfo?.Snippet?.Thumbnails?.High?.Url ?? videoInfo?.Snippet?.Thumbnails?.Medium?.Url ?? "";

            // 2. RECUPERAR EL CANAL DE MONGO
            string targetChannelId = videoInfo?.Snippet?.ChannelId ?? channelIdInfo;

            ZappingChannel canal;

            if (!string.IsNullOrEmpty(targetChannelId))
            {
                canal = await _channelsCollection.Find(c => c.Id == targetChannelId).FirstOrDefaultAsync();
            }
            else
            {
                // Fallback limpio
                canal = await _channelsCollection.Find(c =>
                    (c.Actives != null && c.Actives.ContainsKey(videoId)) ||
                    (c.Upcoming != null && c.Upcoming.ContainsKey(videoId)) ||
                    (c.Past != null && c.Past.ContainsKey(videoId)) ||
                    (c.Discarded != null && c.Discarded.ContainsKey(videoId))
                ).FirstOrDefaultAsync();
            }

            if (canal == null)
            {
                _logger.LogWarning("Video ignorado: El canal {ChannelId} no está registrado en la base de datos.", targetChannelId);
                return;
            }

            // Inicializar diccionarios si vienen null de la BD
            canal.Actives ??= new Dictionary<string, ActiveVideo>();
            canal.Upcoming ??= new Dictionary<string, UpcomingVideo>();
            canal.Past ??= new Dictionary<string, PastVideo>();
            canal.Discarded ??= new Dictionary<string, DiscardedVideo>();

            bool estabaEnActivos = canal.Actives.ContainsKey(videoId);
            bool estabaEnUpcoming = canal.Upcoming.ContainsKey(videoId);
            bool estabaEnPast = canal.Past.ContainsKey(videoId);

            // ESCUDO: Descartar VOD/Reel completamente
            if (!esEnVivo && !esUpcoming && !estabaEnActivos && !estabaEnUpcoming && !estabaEnPast)
            {
                _logger.LogInformation("VOD/Reel detectado en {ChannelName}. Guardando en Discarded y descartando.", canal.ChannelName);

                var discardedVideo = new DiscardedVideo
                {
                    VideoId = videoId,
                    PublishedAt = publishedAt ?? sysTimeNow
                };

                var update = Builders<ZappingChannel>.Update.Set($"Discarded.{videoId}", discardedVideo);
                await _channelsCollection.UpdateOneAsync(c => c.Id == canal.Id, update);

                return;
            }

            bool huboCambiosEnVivos = false;

            // 3. GESTIONAR "ACTIVES" Y TRANSICIONES A "PAST"
            if (esEnVivo)
            {
                canal.Actives.TryGetValue(videoId, out var videoPrevio);
                bool esRealmenteEstreno = videoPrevio?.IsPremiere ?? esEstreno;

                canal.Actives[videoId] = new ActiveVideo
                {
                    VideoId = videoId,
                    Title = videoInfo?.Snippet?.Title ?? (esEstreno ? "Estreno en curso" : "Directo"),
                    ThumbnailUrl = liveImageUrl,
                    IsPremiere = esRealmenteEstreno,
                    PublishedAt = publishedAt,
                    ScheduledStartTime = scheduledStart,
                    ActualStartTime = actualStart ?? sysTimeNow,
                    ActualEndTime = actualEnd,
                    AddedAt = sysTimeNow
                };
                huboCambiosEnVivos = true;
            }
            else if (estabaEnActivos)
            {
                canal.Actives.TryGetValue(videoId, out var videoActivo);
                canal.Actives.Remove(videoId);

                canal.Past[videoId] = new PastVideo
                {
                    VideoId = videoId,
                    Title = videoInfo?.Snippet?.Title ?? videoActivo?.Title ?? "Directo finalizado",
                    ThumbnailUrl = liveImageUrl,
                    WasPremiere = videoActivo?.IsPremiere ?? false,
                    PublishedAt = publishedAt ?? videoActivo?.PublishedAt,
                    ScheduledStartTime = scheduledStart ?? videoActivo?.ScheduledStartTime,
                    ActualStartTime = actualStart ?? videoActivo?.ActualStartTime,
                    ActualEndTime = actualEnd ?? sysTimeNow,
                    EndedAt = sysTimeNow
                };

                _logger.LogInformation("FINALIZADO: El video {VideoId} de {ChannelName} pasó a Past.", videoId, canal.ChannelName);
                huboCambiosEnVivos = true;
            }
            else if (estabaEnPast)
            {
                canal.Past.TryGetValue(videoId, out var videoPast);
                canal.Past[videoId] = new PastVideo
                {
                    VideoId = videoId,
                    Title = videoInfo?.Snippet?.Title ?? videoPast?.Title ?? "Directo finalizado",
                    ThumbnailUrl = !string.IsNullOrEmpty(liveImageUrl) ? liveImageUrl : videoPast?.ThumbnailUrl,
                    WasPremiere = videoPast?.WasPremiere ?? false,
                    PublishedAt = publishedAt ?? videoPast?.PublishedAt,
                    ScheduledStartTime = scheduledStart ?? videoPast?.ScheduledStartTime,
                    ActualStartTime = actualStart ?? videoPast?.ActualStartTime,
                    ActualEndTime = actualEnd ?? videoPast?.ActualEndTime,
                    EndedAt = videoPast?.EndedAt ?? sysTimeNow
                };
            }

            // 4. GESTIONAR "UPCOMING"
            if (esUpcoming)
            {
                canal.Upcoming[videoId] = new UpcomingVideo
                {
                    VideoId = videoId,
                    Title = videoInfo?.Snippet?.Title ?? (esEstreno ? "Estreno Programado" : "Directo Programado"),
                    ThumbnailUrl = liveImageUrl,
                    IsPremiere = esEstreno,
                    PublishedAt = publishedAt,
                    ScheduledStartTime = scheduledStart,
                    ActualStartTime = actualStart,
                    ActualEndTime = actualEnd,
                    AddedAt = sysTimeNow
                };
                _logger.LogInformation("PROGRAMADO: {ChannelName} tiene upcoming ({VideoId}).", canal.ChannelName, videoId);
            }
            else if (estabaEnUpcoming)
            {
                canal.Upcoming.TryGetValue(videoId, out var videoUpcoming);
                canal.Upcoming.Remove(videoId);

                if (esEnVivo)
                {
                    _logger.LogInformation("MUDANZA: El video {VideoId} pasó a EN VIVO.", videoId);
                }
                else
                {
                    canal.Past[videoId] = new PastVideo
                    {
                        VideoId = videoId,
                        Title = videoInfo?.Snippet?.Title ?? videoUpcoming?.Title ?? "Programación cancelada",
                        ThumbnailUrl = liveImageUrl,
                        WasPremiere = videoUpcoming?.IsPremiere ?? false,
                        PublishedAt = publishedAt ?? videoUpcoming?.PublishedAt,
                        ScheduledStartTime = scheduledStart ?? videoUpcoming?.ScheduledStartTime,
                        ActualStartTime = actualStart,
                        ActualEndTime = actualEnd ?? sysTimeNow,
                        EndedAt = sysTimeNow
                    };
                    _logger.LogInformation("CANCELADO: El upcoming {VideoId} se canceló y pasó a Past.", videoId);
                }
            }

            // 5. REGISTRAR ACTIVIDAD
            if (huboCambiosEnVivos)
            {
                canal.LastActivityAt = sysTimeNow;
            }

            // 6. PERSISTIR EN MONGODB
            await _channelsCollection.ReplaceOneAsync(c => c.Id == canal.Id, canal);
            _logger.LogInformation("El canal {ChannelName} fue actualizado con éxito en MongoDB.", canal.ChannelName);
        }
    }
}
