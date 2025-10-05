using Avalonia;
using System;
using Dock.Model.Extensions.DependencyInjection;
using Dock.Serializer;
using EDHelp.ViewModels;
using EDHelp.Models;
using EDHelp.Services;
using EDHelp.ViewModels.Documents;
using EDHelp.ViewModels.Tools;
using EDHelp.Views;
using EDHelp.Views.Documents;
using EDHelp.Views.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace EDHelp;

internal class Program
{
    public static IServiceProvider ServiceProvider;
    
    [STAThread]
    private static void Main(string[] args)
    {
        using var provider = Initialize();
        BuildAvaloniaApp(provider).StartWithClassicDesktopLifetime(args);
    }

    private static ServiceProvider Initialize()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        var provider = services.BuildServiceProvider();
        ServiceProvider = provider;
                
        _ = ServiceProvider.GetService<ICardCacheService>()?.InitializeAsync();
        return provider;
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core app services
        services.AddSingleton<App>();
        services.AddSingleton<ViewLocator>();
        services.AddSingleton<DemoData>();

        // Shared state
        services.AddSingleton<DeckBuilderState>();

        // Document ViewModels (transient - new instance each time)
        services.AddTransient<DeckListViewModel>();
        services.AddTransient<CardPreviewViewModel>();

        // Tool ViewModels (transient)
        services.AddTransient<PopularDecksViewModel>();
        services.AddTransient<CommonCardsViewModel>();
        services.AddTransient<ManaCurveViewModel>();
        services.AddTransient<CombosViewModel>();

        // Main window
        services.AddSingleton<MainWindowViewModel>();

        // Document Views
        services.AddTransient<DeckListView>();
        services.AddTransient<CardPreviewView>();

        // Tool Views
        services.AddTransient<PopularDecksView>();
        services.AddTransient<CommonCardsView>();
        services.AddTransient<ManaCurveView>();
        services.AddTransient<CombosView>();

        // Main window view
        services.AddTransient<MainWindow>();
        
        services.AddSingleton<ICardCacheService, CardCacheService>();
        services.AddSingleton<IMoxfieldService, MoxfieldService>();
        services.AddSingleton<DecklistParser>();
        services.AddSingleton<IScryfallService, ScryfallService>();
        services.AddSingleton<IComboFinderService, ComboFinderService>();

        // Dock infrastructure
        services.AddDock<DockFactory, DockSerializer>();
    }

    public static AppBuilder BuildAvaloniaApp(IServiceProvider provider)
        => AppBuilder.Configure(provider.GetRequiredService<App>)
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure(() => Initialize().GetRequiredService<App>())
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}