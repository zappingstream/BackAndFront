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
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ZappingStreamingDBService
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
    }

    public class ZappingStreamingDBService : BackgroundService
    {
        private readonly HttpClient _httpClient;
        private readonly IMongoDatabase _database;
        private readonly YouTubeService _youtubeService;
        private readonly ILogger<ZappingStreamingDBService> _logger;
        private readonly IHostApplicationLifetime _appLifetime;

        public ZappingStreamingDBService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<ZappingStreamingDBService> logger,
            IHostApplicationLifetime appLifetime)
        {
            _httpClient = httpClient;
            _logger = logger;
            _appLifetime = appLifetime;

            // Configuración de MongoDB
            string mongoUri = configuration["MongoDB:ConnectionString"];
            string dbName = configuration["MongoDB:DatabaseName"] ?? "ZappingStreaming";
            var mongoClient = new MongoClient(mongoUri);
            _database = mongoClient.GetDatabase(dbName);

            // Configuración de YouTube
            string ytApiKey = configuration["YouTube:ApiKey"] ?? "";
            _youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = ytApiKey,
                ApplicationName = "ZappingStreamingWorker"
            });
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("=== INICIANDO TAREAS DE MANTENIMIENTO ===");

                await ProcesarYActualizarCanalesAsync(stoppingToken);
                await RenovarSuscripcionesWebhooksAsync(stoppingToken);

                _logger.LogInformation("=== TODAS LAS TAREAS COMPLETADAS CON ÉXITO ===");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocurrió un error crítico durante la ejecución.");
            }
            finally
            {
                _logger.LogInformation("Apagando la aplicación para finalizar el GitHub Action...");
                _appLifetime.StopApplication();
            }
        }

        private async Task ProcesarYActualizarCanalesAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Obteniendo lista base de canales desde MongoDB...");

            var originCollection = _database.GetCollection<ChannelOriginItem>("origin");
            var canalesValidos = await originCollection
                .Find(c => c.ChannelId != null && c.ChannelId.StartsWith("UC"))
                .ToListAsync(cancellationToken);

            if (!canalesValidos.Any())
            {
                _logger.LogWarning("ChannelsOrigin está vacío o no tiene canales válidos. Abortando sincronización.");
                return;
            }

            _logger.LogInformation("Paso 1: Obteniendo perfiles de {Cantidad} canales desde YouTube...", canalesValidos.Count);

            var lotesDeCanales = canalesValidos.Select(s => s.ChannelId).Chunk(50);
            var infoCanalesYT = new Dictionary<string, Google.Apis.YouTube.v3.Data.Channel>();

            foreach (var lote in lotesDeCanales)
            {
                var request = _youtubeService.Channels.List("snippet,brandingSettings");
                request.Id = string.Join(",", lote);
                var ytResponse = await request.ExecuteAsync(cancellationToken);

                if (ytResponse.Items != null)
                {
                    foreach (var item in ytResponse.Items) infoCanalesYT[item.Id] = item;
                }
            }

            _logger.LogInformation("Paso 2: Rescatando estados de stream en vivo...");

            var channelsCollection = _database.GetCollection<ZappingChannel>("channels");
            var canalesExistentesList = await channelsCollection.Find(_ => true).ToListAsync(cancellationToken);
            var canalesExistentes = canalesExistentesList.ToDictionary(c => c.Id);

            var bulkOps = new List<WriteModel<ZappingChannel>>();

            foreach (var stream in canalesValidos)
            {
                if (infoCanalesYT.TryGetValue(stream.ChannelId, out var channelInfo))
                {
                    string channelName = channelInfo.Snippet.Title;
                    string mongoKey = stream.ChannelId; 

                    bool estabaEnVivo = false;
                    string imgLiveUrlAnterior = "";
                    string lastActivityAnterior = "2000-01-01T00:00:00Z";
                    string videoLiveIdAnterior = "";
                    bool isPremiereAnterior = false;

                    Dictionary<string, UpcomingVideo> upcomingAnterior = null;
                    Dictionary<string, ActiveVideo> activesAnterior = null;
                    Dictionary<string, PastVideo> pastAnterior = null;

                    if (canalesExistentes.TryGetValue(mongoKey, out var canalAnterior))
                    {
                        estabaEnVivo = canalAnterior.ChannelLive;
                        imgLiveUrlAnterior = canalAnterior.ChannelImgLiveUrl ?? "";
                        lastActivityAnterior = string.IsNullOrEmpty(canalAnterior.LastActivityAt)
                            ? "2000-01-01T00:00:00Z"
                            : canalAnterior.LastActivityAt;
                        videoLiveIdAnterior = canalAnterior.LiveVideoId ?? "";
                        isPremiereAnterior = canalAnterior.IsPremiere;

                        upcomingAnterior = canalAnterior.Upcoming;
                        activesAnterior = canalAnterior.Actives;
                        pastAnterior = canalAnterior.Past;
                    }

                    string imageUrl = channelInfo.Snippet.Thumbnails.High?.Url
                                   ?? channelInfo.Snippet.Thumbnails.Medium?.Url
                                   ?? channelInfo.Snippet.Thumbnails.Default__?.Url ?? "";

                    string bannerUrl = channelInfo.BrandingSettings?.Image?.BannerExternalUrl ?? "";

                    var canalActualizado = new ZappingChannel
                    {
                        Id = mongoKey,
                        ChannelName = channelName,
                        ChannelLiveUrl = $"https://www.youtube.com/channel/{stream.ChannelId}/live",
                        ChannelImgUrl = imageUrl,
                        ChannelDescription = channelInfo.Snippet.Description,
                        ChannelBannerUrl = bannerUrl,
                        ChannelCity = stream.City,
                        ChannelType = stream.Category,

                        ChannelLive = estabaEnVivo,
                        ChannelImgLiveUrl = imgLiveUrlAnterior,
                        LastActivityAt = lastActivityAnterior,
                        LiveVideoId = videoLiveIdAnterior,
                        IsPremiere = isPremiereAnterior,

                        Upcoming = upcomingAnterior,
                        Actives = activesAnterior,
                        Past = pastAnterior
                    };

                    // Preparamos un Upsert (Si existe lo pisa, si no, lo inserta)
                    var upsert = new ReplaceOneModel<ZappingChannel>(
                        Builders<ZappingChannel>.Filter.Eq(c => c.Id, mongoKey),
                        canalActualizado)
                    {
                        IsUpsert = true
                    };

                    bulkOps.Add(upsert);
                }
            }

            if (bulkOps.Any())
            {
                _logger.LogInformation("Paso 3: Realizando BulkWrite de {Cantidad} canales a MongoDB...", bulkOps.Count);
                await channelsCollection.BulkWriteAsync(bulkOps, cancellationToken: cancellationToken);

                // Actualizamos la metadata
                var metaCollection = _database.GetCollection<BsonDocument>("Metadata");
                var metaFilter = Builders<BsonDocument>.Filter.Eq("_id", "SystemStats");
                var metaUpdate = Builders<BsonDocument>.Update.Set("LastSynced", DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));
                await metaCollection.UpdateOneAsync(metaFilter, metaUpdate, new UpdateOptions { IsUpsert = true }, cancellationToken);

                _logger.LogInformation("Base de datos sincronizada.");
            }
        }

        private async Task RenovarSuscripcionesWebhooksAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Paso 4: Renovando suscripciones a Webhooks de YouTube...");
            try
            {
                var originCollection = _database.GetCollection<ChannelOriginItem>("origin");
                var canalesValidos = await originCollection
                    .Find(c => c.ChannelId != null && c.ChannelId.StartsWith("UC"))
                    .ToListAsync(cancellationToken);

                foreach (var str in canalesValidos)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    var values = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("hub.mode", "subscribe"),
                        new KeyValuePair<string, string>("hub.topic", $"https://www.youtube.com/xml/feeds/videos.xml?channel_id={str.ChannelId}"),
                        new KeyValuePair<string, string>("hub.callback", "https://zappingstreamlivewebhook.onrender.com/webhook")
                    });

                    try
                    {
                        var response = await _httpClient.PostAsync("https://pubsubhubbub.appspot.com/subscribe", values, cancellationToken);
                        if (!response.IsSuccessStatusCode)
                        {
                            _logger.LogWarning("Fallo al suscribir {Channel}: {Status}", str.Title, response.StatusCode);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Error en petición para {Channel}: {Message}", str.Title, ex.Message);
                    }

                    await Task.Delay(500, cancellationToken);
                }
                _logger.LogInformation("Renovación de webhooks finalizada.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al intentar renovar suscripciones.");
            }
        }

        private string SanitizarKey(string key)
        {
            // MongoDB no tiene los mismos problemas con caracteres que Firebase, 
            // pero si tu frontend espera los IDs formateados de esta manera, se mantiene.
            if (string.IsNullOrWhiteSpace(key)) return "UnknownChannel";
            return Regex.Replace(key, @"[.#$\[\]]", "").Trim();
        }
    }
}