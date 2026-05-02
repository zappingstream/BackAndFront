using Firebase.Database;
using Firebase.Database.Query;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Serialization;
using Newtonsoft.Json; // Agregado para el mapeo de Firebase
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ZappingStreamingDBService
{
    public class ChannelOriginItem // Renombrado de GithubStreamItem
    {
        [JsonProperty("title")] // Newtonsoft (Firebase)
        [JsonPropertyName("title")] // System.Text.Json
        public string Title { get; set; }

        [JsonProperty("channelId")]
        [JsonPropertyName("channelId")]
        public string ChannelId { get; set; }

        [JsonProperty("city")]
        [JsonPropertyName("city")]
        public string City { get; set; }

        [JsonProperty("category")]
        [JsonPropertyName("category")]
        public string Category { get; set; }
    }

    public class FirebaseChannel
    {
        public string ChannelName { get; set; }
        public string ChannelDescription { get; set; }
        public string ChannelCity { get; set; }
        public string ChannelType { get; set; }
        public string ChannelLiveUrl { get; set; }
        public string ChannelImgUrl { get; set; }
        public string LastActivityAt { get; set; }

        // --- PROPIEDADES LEGACY (Mantenidas para compatibilidad temporal con el Front) ---
        public bool ChannelLive { get; set; }
        public string ChannelImgLiveUrl { get; set; }
        public string LiveVideoId { get; set; }
        public bool IsPremiere { get; set; }

        // --- NUEVAS COLECCIONES MULTI-ESTADO ---
        public Dictionary<string, UpcomingVideo> Upcoming { get; set; }
        public Dictionary<string, ActiveVideo> Actives { get; set; }
    }

    public class UpcomingVideo
    {
        public string VideoId { get; set; }
        public string Title { get; set; }
        public string ScheduledStartTime { get; set; }
        public string ThumbnailUrl { get; set; }
        public string AddedAt { get; set; }
        public bool Live { get; set; }
        public bool IsPremiere { get; set; }
    }

    public class ActiveVideo
    {
        public string VideoId { get; set; }
        public string Title { get; set; }
        public string ScheduledStartTime { get; set; }
        public string ThumbnailUrl { get; set; }
        public string AddedAt { get; set; }
        public bool Live { get; set; }
        public bool IsPremiere { get; set; }
    }

    public class ZappingStreamingDBService : BackgroundService
    {
        private readonly HttpClient _httpClient;
        private readonly FirebaseClient _firebaseClient;
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

            string firebaseUrl = configuration["Firebase:Url"] ?? "https://zappingstreaming-default-rtdb.firebaseio.com/";
            string ytApiKey = configuration["YouTube:ApiKey"] ?? "";
            string firebaseSecret = configuration["Firebase:Secret"] ?? "";

            var options = new FirebaseOptions
            {
                AuthTokenAsyncFactory = () => Task.FromResult(firebaseSecret)
            };

            _firebaseClient = new FirebaseClient(firebaseUrl, options);

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
            _logger.LogInformation("Obteniendo lista base de canales desde ChannelsOrigin en Firebase...");

            // Reemplazo: Consulta directa a ChannelsOrigin en vez de GitHub
            var streams = await _firebaseClient.Child("ChannelsOrigin").OnceSingleAsync<List<ChannelOriginItem>>();

            // (Opcional  recomendado) Firebase a veces deja valores "null" en los arrays si borrás algún elemento. 
            streams = streams?.Where(x => x != null).ToList() ?? new List<ChannelOriginItem>();

            var canalesValidos = streams
                .Where(s => !string.IsNullOrEmpty(s.ChannelId) && s.ChannelId.StartsWith("UC") && s.ChannelId.Length > 2)
                .ToList();

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
                var request = _youtubeService.Channels.List("snippet");
                request.Id = string.Join(",", lote);
                var ytResponse = await request.ExecuteAsync(cancellationToken);

                if (ytResponse.Items != null)
                {
                    foreach (var item in ytResponse.Items) infoCanalesYT[item.Id] = item;
                }
            }

            _logger.LogInformation("Paso 2: Rescatando estados de stream en vivo desde Firebase...");
            var estadoActualFirebase = await _firebaseClient.Child("Channels").OnceAsync<FirebaseChannel>();
            var canalesExistentes = estadoActualFirebase.ToDictionary(x => x.Key, x => x.Object);

            var canalesParaFirebase = new Dictionary<string, FirebaseChannel>();

            foreach (var stream in canalesValidos)
            {
                if (infoCanalesYT.TryGetValue(stream.ChannelId, out var channelInfo))
                {
                    string channelName = channelInfo.Snippet.Title;
                    string firebaseKey = SanitizarKeyFirebase(channelName);

                    // Variables Legacy
                    bool estabaEnVivo = false;
                    string imgLiveUrlAnterior = "";
                    string lastActivityAnterior = "";
                    string videoLiveIdAnterior = "";
                    bool isPremiereAnterior = false;

                    // Colecciones
                    Dictionary<string, UpcomingVideo> upcomingAnterior = null;
                    Dictionary<string, ActiveVideo> activesAnterior = null;

                    if (canalesExistentes.TryGetValue(firebaseKey, out var canalAnterior))
                    {
                        estabaEnVivo = canalAnterior.ChannelLive;
                        imgLiveUrlAnterior = canalAnterior.ChannelImgLiveUrl ?? "";
                        lastActivityAnterior = string.IsNullOrEmpty(canalAnterior.LastActivityAt)
                            ? "2000-01-01T00:00:00Z"
                            : canalAnterior.LastActivityAt;
                        videoLiveIdAnterior = canalAnterior.LiveVideoId ?? "";
                        isPremiereAnterior = canalAnterior.IsPremiere;

                        // RESCATAMOS LAS COLECCIONES
                        upcomingAnterior = canalAnterior.Upcoming;
                        activesAnterior = canalAnterior.Actives;
                    }
                    else
                    {
                        lastActivityAnterior = "2000-01-01T00:00:00Z";
                    }

                    string imageUrl = channelInfo.Snippet.Thumbnails.High?.Url
                                   ?? channelInfo.Snippet.Thumbnails.Medium?.Url
                                   ?? channelInfo.Snippet.Thumbnails.Default__?.Url ?? "";

                    canalesParaFirebase[firebaseKey] = new FirebaseChannel
                    {
                        ChannelName = channelName,
                        ChannelLiveUrl = $"https://www.youtube.com/channel/{stream.ChannelId}/live",
                        ChannelImgUrl = imageUrl,
                        ChannelDescription = channelInfo.Snippet.Description,
                        ChannelCity = stream.City,
                        ChannelType = stream.Category,

                        // Mantenemos las properties legacy para compatibilidad
                        ChannelLive = estabaEnVivo,
                        ChannelImgLiveUrl = imgLiveUrlAnterior,
                        LastActivityAt = lastActivityAnterior,
                        LiveVideoId = videoLiveIdAnterior,
                        IsPremiere = isPremiereAnterior,

                        // DEVOLVEMOS LAS LISTAS INTACTAS AL FIREBASE
                        Upcoming = upcomingAnterior,
                        Actives = activesAnterior
                    };
                }
            }

            if (canalesParaFirebase.Any())
            {
                _logger.LogInformation("Paso 3: Subiendo {Cantidad} canales a Firebase...", canalesParaFirebase.Count);

                await _firebaseClient.Child("Channels").PutAsync(canalesParaFirebase);
                await _firebaseClient.Child("Meta").PutAsync(new { LastSynced = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") });

                _logger.LogInformation("Base de datos sincronizada.");
            }
        }

        private async Task RenovarSuscripcionesWebhooksAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Paso 4: Renovando suscripciones a Webhooks de YouTube...");
            try
            {
                // Reemplazo: Consulta a Firebase en lugar del fetch HTTP a GitHub
                var streams = await _firebaseClient.Child("ChannelsOrigin").OnceSingleAsync<List<ChannelOriginItem>>();

                streams = streams?.Where(x => x != null).ToList() ?? new List<ChannelOriginItem>();

                var canalesValidos = streams.Where(s => !string.IsNullOrEmpty(s.ChannelId) && s.ChannelId.StartsWith("UC")).ToList();

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

        private string SanitizarKeyFirebase(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return "UnknownChannel";
            return Regex.Replace(key, @"[.#$\[\]]", "").Trim();
        }
    }
}