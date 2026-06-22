using Microsoft.Extensions.DependencyInjection;
using Phoodab.Mobile.Shared;

namespace Phoodab.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddPhoodabSharedPresentation();
        builder.Services.AddSingleton<MainPage>();

        return builder.Build();
    }
}
