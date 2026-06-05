using Microsoft.Extensions.DependencyInjection;
using Phoodab.Application;

namespace Phoodab.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddSingleton<IUtcDateProvider, SystemUtcDateProvider>();
        builder.Services.AddSingleton<ReplenishmentSuggestionService>();
        builder.Services.AddSingleton<IInventoryMvpStore, FileInventoryMvpStore>();
        builder.Services.AddSingleton<MainPage>();

        return builder.Build();
    }
}
