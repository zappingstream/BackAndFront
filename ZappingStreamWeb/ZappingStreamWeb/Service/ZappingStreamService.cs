using System.Net.Http.Json;
using Microsoft.JSInterop; 

namespace ZappingStreamWebServer.Service
{
    public class FirebaseChannel
    {
        public string ChannelName { get; set; } = string.Empty;
        public bool ChannelLive { get; set; } = false;
        public string ChannelLiveUrl { get; set; } = string.Empty;
        public string ChannelImgUrl { get; set; } = string.Empty;
        public string ChannelImgLiveUrl { get; set; } = string.Empty;
        public string ChannelDescription { get; set; } = string.Empty;
        public string ChannelCity { get; set; } = string.Empty;
        public string ChannelType { get; set; } = string.Empty;
        public DateTime LastActivityAt { get; set; } = DateTime.MinValue;
    }

    public class FirebaseDBMeta
    {
        public DateTime LastSynced { get; set; } = DateTime.MinValue;
    }

    public class ZappingStreamService
    {
        private readonly HttpClient _httpClient;
        private readonly IJSRuntime _jsRuntime; // 1. Agregamos el JSRuntime

        private const string FirebaseDbUrl = "https://zappingstreaming-default-rtdb.firebaseio.com/Channels.json";
        private const string FirebaseDbLastSync = "https://zappingstreaming-default-rtdb.firebaseio.com/Meta.json";

        // 2. Inyectamos el IJSRuntime en el constructor
        public ZappingStreamService(HttpClient httpClient, IJSRuntime jsRuntime)
        {
            _httpClient = httpClient;
            _jsRuntime = jsRuntime;
        }

        // 3. Este es el método mágico que busca el token y lo inyecta
        private async Task AttachAppCheckTokenAsync()
        {
            try
            {
                // Llamamos a la función de JavaScript que creamos recién
                var token = await _jsRuntime.InvokeAsync<string>("getFirebaseAppCheckToken");

                if (!string.IsNullOrEmpty(token))
                {
                    // Limpiamos por si ya existía de una petición anterior
                    _httpClient.DefaultRequestHeaders.Remove("X-Firebase-AppCheck");
                    // Adjuntamos el token como Header
                    _httpClient.DefaultRequestHeaders.Add("X-Firebase-AppCheck", token);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adjuntando el token de App Check: {ex.Message}");
            }
        }

        public async Task<FirebaseDBMeta> GetLastSyncDateTime()
        {
            // 4. Antes de hacer la petición, adjuntamos el token de seguridad
            await AttachAppCheckTokenAsync();

            try
            {
                var firebaseMetaData = await _httpClient.GetFromJsonAsync<FirebaseDBMeta>(FirebaseDbLastSync);

                if (firebaseMetaData != null)
                {
                    return firebaseMetaData;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al intentar descargar la metadata de Firebase: {ex.Message}");
            }

            return new FirebaseDBMeta();
        }

        public async Task<List<FirebaseChannel>> GetChannelsAsync()
        {
            // 5. Acá también adjuntamos el token de seguridad antes de pedir la grilla
            await AttachAppCheckTokenAsync();

            try
            {
                var firebaseData = await _httpClient.GetFromJsonAsync<Dictionary<string, FirebaseChannel>>(FirebaseDbUrl);

                if (firebaseData != null)
                {
                    return firebaseData.Values.ToList();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al intentar descargar la grilla de Firebase: {ex.Message}");
            }

            return new List<FirebaseChannel>();
        }
    }
}