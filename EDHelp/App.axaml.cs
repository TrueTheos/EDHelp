using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Markup.Xaml;
using EDHelp.Controls;
using EDHelp.Services;
using EDHelp.ViewModels;
using EDHelp.Views;
using Microsoft.Extensions.DependencyInjection;

namespace EDHelp;

public partial class App : Application
{
    public static ServiceProvider serviceProvider { get; private set; }
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        
        SetupServices();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            var cardCacheService = serviceProvider.GetRequiredService<ICardCacheService>();
            var parser = serviceProvider.GetRequiredService<DecklistParser>();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(cardCacheService, parser),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
    
    private void SetupServices()
    {
        var collection = new ServiceCollection();
        
        collection.AddSingleton<ICardCacheService, CardCacheService>();
        collection.AddSingleton<IMoxfieldService, MoxfieldService>();
        collection.AddSingleton<DecklistParser>();
        collection.AddSingleton<IScryfallService, ScryfallService>();

        collection.AddSingleton<DeckBuilderViewModel>();
        collection.AddSingleton<MainWindowViewModel>();
        collection.AddSingleton<CachedCardImage>();
        
        serviceProvider = collection.BuildServiceProvider();

        _ = serviceProvider.GetService<CardCacheService>()?.InitializeAsync();
    }
}