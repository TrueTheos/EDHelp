using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using EDHelp.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace EDHelp.Views;

public partial class DeckBuilderView : UserControl
{
    public DeckBuilderView()
    {
        InitializeComponent();

        DeckBuilderViewModel vm = App.serviceProvider.GetRequiredService<DeckBuilderViewModel>();
        
        this.Find<AutoCompleteBox>("CardSearchBox").AsyncPopulator = vm.UpdateSearchList;
    }
}