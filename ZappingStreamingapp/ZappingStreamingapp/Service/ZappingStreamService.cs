using Microsoft.JSInterop;
using System.Net.Http.Json;

namespace ZappingStreamingapp.Service
{
    // --- MODELOS DE DATOS ---
    public class FirebaseChannel
    {
        public string ChannelName { get; set; }
        public string ChannelDescription { get; set; }
        public string ChannelCity { get; set; }
        public string ChannelType { get; set; }
        public string ChannelLiveUrl { get; set; }
        public string ChannelImgUrl { get; set; }

        // Legacy
        public string ChannelImgLiveUrl { get; set; }
        public bool ChannelLive { get; set; }
        public string LiveVideoId { get; set; }
        public DateTime LastActivityAt { get; set; }
        public bool IsPremiere { get; set; }
    }

    public class FirebaseDBMeta
    {
        public DateTime LastSynced { get; set; } = DateTime.MinValue;
    }

    public class ZappingStreamService
    {
        private readonly HttpClient _httpClient;

        private const string FirebaseDbUrl = "https://zappingstreaming-default-rtdb.firebaseio.com/Channels.json";
        private const string FirebaseDbLastSync = "https://zappingstreaming-default-rtdb.firebaseio.com/Meta.json";

        // 2. Inyectamos el IJSRuntime en el constructor
        public ZappingStreamService(HttpClient httpClient, IJSRuntime jsRuntime)
        {
            _httpClient = httpClient;
        }


        public async Task<FirebaseDBMeta> GetLastSyncDateTime()
        {
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