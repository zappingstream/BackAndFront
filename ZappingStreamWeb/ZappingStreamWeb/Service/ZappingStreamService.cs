using System.Net.Http.Json;
using Microsoft.JSInterop; 

namespace ZappingStreamWebServer.Service
{
    // --- MODELO DE DATOS (Parcial) ---
    public class FirebaseChannel
    {
        // El nombre del canal
        public string ChannelName { get; set; }
        // La descripción del canal
        public string ChannelDescription { get; set; }
        // La ciudad donde transmite el canal
        public string ChannelCity { get; set; }
        // El tipo de canal: Stream, Personal, Television, Radio
        public string ChannelType { get; set; }
        // La url live del canal
        public string ChannelLiveUrl { get; set; }
        // El logo del canal
        public string ChannelImgUrl { get; set; }
        // La imagen del video live del canal
        public string ChannelImgLiveUrl { get; set; }
        // booleano que indica si un canal está en vivo o no
        public bool ChannelLive { get; set; }
        // El videoId del vivo (Sirve unicamente para los estrenos, el link para los vivos se hace con ChannelId/live
        public string LiveVideoId { get; set; }
        // La última actividad del canal
        public DateTime LastActivityAt { get; set; }
        // Indica si es un estreno
        public bool IsPremiere { get; set; } 
    }

    // La última fecha en que la base fué sincronizada
    public class FirebaseDBMeta
    {
        public DateTime LastSynced { get; set; } = DateTime.MinValue;
    }

    // El servicio
    public class ZappingStreamService
    {
        // Agregamos el JSRuntime
        private readonly IJSRuntime _jsRuntime;


        // Inyectamos el IJSRuntime en el constructor
        public ZappingStreamService(HttpClient httpClient, IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

      
        // Función que devuelve la metadata de la base de datos
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

        // Función que devuelve asincrónicamente todos los canales
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