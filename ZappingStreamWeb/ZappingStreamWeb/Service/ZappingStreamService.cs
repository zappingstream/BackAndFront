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
        private readonly IJSRuntime _jsRuntime; // 1. Agregamos el JSRuntime


        // 2. Inyectamos el IJSRuntime en el constructor
        public ZappingStreamService(HttpClient httpClient, IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        // 3. Este es el método mágico que busca el token y lo inyecta
      
        public async Task<FirebaseDBMeta> GetLastSyncDateTime()
        {
            // 4. Antes de hacer la petición, adjuntamos el token de seguridad

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
            // 5. Acá también adjuntamos el token de seguridad antes de pedir la grilla

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