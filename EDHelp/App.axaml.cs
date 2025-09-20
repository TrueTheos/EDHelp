using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using System.Net.Http;
using Avalonia.Markup.Xaml;
using EDHelp.Services;
using EDHelp.ViewModels;
using EDHelp.Views;
using Microsoft.Extensions.DependencyInjection;

namespace EDHelp;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    
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
            var cardCacheService = _serviceProvider?.GetRequiredService<CardCacheService>();
            desktop.MainWindow = new MainWindow
            {
                
                DataContext = new MainWindowViewModel(cardCacheService),
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
        var services = new ServiceCollection();
        
        services.AddSingleton<HttpClient>();
        services.AddSingleton<CardCacheService>();
        
        _serviceProvider = services.BuildServiceProvider();
    }
}