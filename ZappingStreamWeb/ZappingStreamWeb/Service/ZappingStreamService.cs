using System.Net.Http.Json;

namespace ZappingStreamWebServer.Service
{
    // 1. Reutilizamos el mismo modelo que tenías en el Worker
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

        // La URL directa a tu nodo, terminada en .json
        private const string FirebaseDbUrl = "https://zappingstreaming-default-rtdb.firebaseio.com/Channels.json";
        private const string FirebaseDbLastSync = "https://zappingstreaming-default-rtdb.firebaseio.com/Meta.json";

        public ZappingStreamService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<FirebaseDBMeta> GetLastSyncDateTime()
        {
            try
            {
                // 2. Firebase devuelve un Diccionario<String, Objeto>
                var firebaseMetaData = await _httpClient.GetFromJsonAsync<FirebaseDBMeta>(FirebaseDbLastSync);

                if (firebaseMetaData != null)
                {
                    // 3. Extraemos solo los valores (los canales) y los pasamos a una lista plana
                    return firebaseMetaData;
                }
            }
            catch (Exception ex)
            {
                // Ideal para inyectar ILogger más adelante
                Console.WriteLine($"Error al intentar descargar la metadata de Firebase: {ex.Message}");
            }

            // Si algo falla, devolvemos una lista vacía para que no explote la vista
            return new FirebaseDBMeta();
        }

        public async Task<List<FirebaseChannel>> GetChannelsAsync()
        {
            try
            {
                // 2. Firebase devuelve un Diccionario<String, Objeto>
                var firebaseData = await _httpClient.GetFromJsonAsync<Dictionary<string, FirebaseChannel>>(FirebaseDbUrl);

                if (firebaseData != null)
                {
                    // 3. Extraemos solo los valores (los canales) y los pasamos a una lista plana
                    return firebaseData.Values.ToList();
                }
            }
            catch (Exception ex)
            {
                // Ideal para inyectar ILogger más adelante
                Console.WriteLine($"Error al intentar descargar la grilla de Firebase: {ex.Message}");
            }

            // Si algo falla, devolvemos una lista vacía para que no explote la vista
            return new List<FirebaseChannel>();
        }
    }
}