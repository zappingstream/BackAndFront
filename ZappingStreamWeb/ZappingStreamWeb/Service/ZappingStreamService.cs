using System.Net.Http.Json;
using Microsoft.JSInterop; 

namespace ZappingStreamWebServer.Service
{
    public class FirebaseChannel
    {
        public string ChannelName { get; set; }
        public string ChannelDescription { get; set; }
        public string ChannelCity { get; set; }
        public string ChannelType { get; set; }
        public string ChannelLiveUrl { get; set; }
        public string ChannelImgUrl { get; set; }
        public DateTime LastActivityAt { get; set; }

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