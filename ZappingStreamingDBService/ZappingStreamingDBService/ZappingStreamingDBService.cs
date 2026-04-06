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
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ZappingStreamingDBService
{
    public class GithubStreamItem
    {
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("channelId")]
        public string ChannelId { get; set; }

        [JsonPropertyName("city")]
        public string City { get; set; }

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
        public string ChannelImgLiveUrl { get; set; }
        public bool ChannelLive { get; set;  }
        public string LastActivityAt { get; set; }
    }

    public class ZappingStreamingDBService : BackgroundService
    {
        private readonly HttpClient _httpClient;
        private readonly FirebaseClient _firebaseClient;
        private readonly YouTubeService _youtubeService;
        private readonly ILogger<ZappingStreamingDBService> _logger;
        private readonly IHostApplicationLifetime _appLifetime;

        private const string JsonUrl = "https://raw.githubusercontent.com/zappingstreaming/argstreams/refs/heads/main/argstreams.json";

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
                _logger.LogInformation("Iniciando sincronización de canales...");
                await ProcesarYActualizarCanalesAsync(stoppingToken);
                _logger.LogInformation("Sincronización completada exitosamente.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocurrió un error durante la sincronización de canales.");
            }
            finally
            {
                _logger.LogInformation("Apagando el servicio para finalizar la tarea...");
                _appLifetime.StopApplication();
            }
        }

        private async Task ProcesarYActualizarCanalesAsync(CancellationToken cancellationToken)
        {
            var response = await _httpClient.GetStringAsync(JsonUrl, cancellationToken);
            var streams = JsonSerializer.Deserialize<List<GithubStreamItem>>(response) ?? new List<GithubStreamItem>();

            if (!streams.Any())
            {
                _logger.LogWarning("El JSON de GitHub se descargó pero está vacío o no se pudo deserializar.");
                return;
            }

            // AHORA GUARDAMOS UNA LISTA DE IDs POR CANAL
            var activeStreams = new List<(GithubStreamItem Stream, DateTimeOffset LastActivity, List<string> VideoIds)>();
            var limiteInactividad = DateTimeOffset.UtcNow.AddYears(-1);

            _logger.LogInformation("Paso 1: Verificando actividad vía XML Feed para {Cantidad} canales...", streams.Count);

            foreach (var stream in streams)
            {
                string channelId = stream.ChannelId;

                if (string.IsNullOrEmpty(channelId) || !channelId.StartsWith("UC") || channelId.Length <= 2)
                    continue;

                try
                {
                    string feedUrl = $"https://www.youtube.com/feeds/videos.xml?channel_id={channelId}";
                    string xmlContent = await _httpClient.GetStringAsync(feedUrl, cancellationToken);

                    var xDoc = XDocument.Parse(xmlContent);
                    XNamespace atom = "http://www.w3.org/2005/Atom";
                    XNamespace yt = "http://www.youtube.com/xml/schemas/2015";

                    var entries = xDoc.Descendants(atom + "entry").ToList();

                    if (entries.Any())
                    {
                        // La última actividad general sigue siendo el primer video de la lista (el más nuevo)
                        var lastActivityStr = entries
                            .SelectMany(e => new[]
                            {
                                DateTimeOffset.Parse(e.Element(atom + "published")?.Value),
                            })
                            .Max()
                            .ToString("o");

                        if (DateTimeOffset.TryParse(lastActivityStr, out var fechaActividad))
                        {
                            if (fechaActividad >= limiteInactividad)
                            {
                                // Recolectamos TODOS los IDs de los videos recientes de este canal (hasta 15)
                                var videoIds = entries
                                    .Select(e => e.Element(yt + "videoId")?.Value)
                                    .Where(id => !string.IsNullOrEmpty(id))
                                    .ToList();

                                activeStreams.Add((stream, fechaActividad, videoIds));
                            }
                            else
                            {
                                _logger.LogInformation("Canal ignorado por inactividad (> 1 año): {ChannelName}", stream.Title);
                            }
                        }
                    }
                }
                catch (HttpRequestException)
                {
                    _logger.LogWarning("Feed XML no disponible (404/Error) para el canal {ChannelId}.", channelId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al parsear el XML del canal {ChannelId}", channelId);
                }
            }

            if (!activeStreams.Any())
            {
                _logger.LogWarning("No quedaron canales activos. Finalizando.");
                return;
            }

            _logger.LogInformation("Paso 2: Verificando cuáles de los videos recientes están en vivo AHORA...");

            var videosEnVivo = new Dictionary<string, Google.Apis.YouTube.v3.Data.Video>();

            // Juntamos absolutamente todos los IDs de todos los canales y los agrupamos de a 50
            var todosLosVideosIds = activeStreams
                .SelectMany(s => s.VideoIds)
                .Distinct()
                .Chunk(50);

            foreach (var lote in todosLosVideosIds)
            {
                var videoRequest = _youtubeService.Videos.List("snippet,contentDetails");
                videoRequest.Id = string.Join(",", lote);

                var videoResponse = await videoRequest.ExecuteAsync(cancellationToken);

                if (videoResponse.Items != null)
                {
                    foreach (var video in videoResponse.Items)
                    {
                        if (video.Snippet?.LiveBroadcastContent == "live" && video.ContentDetails.Duration == "P0D")
                        {
                            videosEnVivo.Add(video.Id, video);
                        }
                    }
                }
            }

            _logger.LogInformation("Encontramos {Cantidad} videos transmitiendo en vivo en este momento.", videosEnVivo.Count);
            _logger.LogInformation("Paso 3: Obteniendo perfiles de los {Cantidad} canales activos...", activeStreams.Count);

            var lotesDeCanales = activeStreams
                .Select(s => s.Stream.ChannelId)
                .Chunk(50);

            var infoCanalesYT = new Dictionary<string, Google.Apis.YouTube.v3.Data.Channel>();

            foreach (var lote in lotesDeCanales)
            {
                var request = _youtubeService.Channels.List("snippet");
                request.Id = string.Join(",", lote);

                var ytResponse = await request.ExecuteAsync(cancellationToken);

                if (ytResponse.Items != null)
                {
                    foreach (var item in ytResponse.Items)
                    {
                        infoCanalesYT[item.Id] = item;
                    }
                }
            }

            var canalesParaFirebase = new Dictionary<string, FirebaseChannel>();

            foreach (var item in activeStreams)
            {
                var stream = item.Stream;
                string channelId = stream.ChannelId;

                if (infoCanalesYT.TryGetValue(channelId, out var channelInfo))
                {
                    // 1. Buscamos si existe algún ID de video de este canal dentro de nuestro diccionario de vivos
                    string idVideoEnVivo = item.VideoIds.FirstOrDefault(id => videosEnVivo.ContainsKey(id));
                    bool estaEnVivo = !string.IsNullOrEmpty(idVideoEnVivo);

                    // 2. Determinamos la URL de la imagen correctamente
                    string imageUrl;
                    string liveImageUrl = "";
                    imageUrl = channelInfo.Snippet.Thumbnails.High.Url
                            ?? channelInfo.Snippet.Thumbnails.Medium.Url
                            ?? channelInfo.Snippet.Thumbnails.Default__.Url;
                    if (estaEnVivo && idVideoEnVivo != null)
                    {
                        // Si está en vivo, extraemos la miniatura del VIDEO
                        var video = videosEnVivo[idVideoEnVivo];
                        liveImageUrl = video.Snippet.Thumbnails.High.Url
                                    ?? video.Snippet.Thumbnails.Medium.Url
                                    ?? video.Snippet.Thumbnails.Default__.Url; // Default__ tiene doble guion bajo en la SDK de .NET
                    
                    }
                    

                    var canalActualizado = new FirebaseChannel
                    {
                        ChannelName = channelInfo.Snippet.Title,
                        ChannelLiveUrl = $"https://www.youtube.com/channel/{channelId}/live",
                        ChannelImgUrl = imageUrl, // Asignamos la imagen calculada arriba
                        ChannelImgLiveUrl = liveImageUrl,
                        ChannelDescription = channelInfo.Snippet.Description,
                        ChannelCity = stream.City,
                        ChannelType = stream.Category,
                        ChannelLive = estaEnVivo,
                        LastActivityAt = item.LastActivity.ToString("yyyy-MM-ddTHH:mm:ssZ")
                    };

                    string firebaseKey = SanitizarKeyFirebase(canalActualizado.ChannelName);
                    canalesParaFirebase[firebaseKey] = canalActualizado;
                }
            }

            if (canalesParaFirebase.Any())
            {
                _logger.LogInformation("Paso 4: Subiendo {Cantidad} canales a Firebase (Reemplazo total)...", canalesParaFirebase.Count);

                await _firebaseClient
                    .Child("Channels")
                    .PutAsync(canalesParaFirebase);

                // 2. NUEVO: Subimos la fecha de última sincronización
                // Lo guardamos en un nodo aparte para que el Front lo lea fácil
                var metaData = new
                {
                    LastSynced = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                };

                await _firebaseClient
                    .Child("Meta")
                    .PutAsync(metaData);

                _logger.LogInformation("Base de datos y metadata sincronizadas correctamente.");
            }
        }

        private string SanitizarKeyFirebase(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return "UnknownChannel";
            return Regex.Replace(key, @"[.#$\[\]]", "").Trim();
        }
    }
}