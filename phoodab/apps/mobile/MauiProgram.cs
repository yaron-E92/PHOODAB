using Microsoft.Extensions.DependencyInjection;

namespace Phoodab.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddSingleton(new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        });
        builder.Services.AddSingleton<MainPage>();

        return builder.Build();
    }
}
