using Microsoft.Extensions.Logging;
using ZappingStreamingapp.Service;
using ZappingStreamingapp.Service;

namespace ZappingStreamingapp
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            // 1. Inyectamos un HttpClient "desnudo", sin BaseAddress. 
            // Tu servicio ya tiene las URLs completas de Firebase, no necesita más.
            builder.Services.AddScoped(sp => new HttpClient());

            // 2. Registramos tu servicio de canales
            builder.Services.AddScoped<ZappingStreamService>();

            // 3. Devolvemos la app construida (SIN el RunAsync que rompe todo)
            return builder.Build();
        }
    }
}