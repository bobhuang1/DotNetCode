#if MAUI
#if MAUI
using Microsoft.Extensions.Logging;
using ShopCommon;

namespace ShopMobile;

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
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton(_ => ShopApiClient.CreateDefault());

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
#endif
#endif
