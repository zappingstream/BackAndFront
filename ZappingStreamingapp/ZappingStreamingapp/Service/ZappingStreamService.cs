using Microsoft.JSInterop;

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
        private readonly IJSRuntime _jsRuntime;

        // Inyectamos SOLO el IJSRuntime (ya no usamos HttpClient para las llamadas a la DB)
        public ZappingStreamService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public async Task<FirebaseDBMeta> GetLastSyncDateTime()
        {
            try
            {
                var firebaseMetaData = await _jsRuntime.InvokeAsync<FirebaseDBMeta>("traerMetaFirebase");

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
                var firebaseData = await _jsRuntime.InvokeAsync<Dictionary<string, FirebaseChannel>>("traerCanalesFirebase");

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