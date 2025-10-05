using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using EDHelp.ViewModels.Documents;
using Microsoft.Extensions.DependencyInjection;

namespace EDHelp.Views.Documents;

public partial class DeckListView : UserControl
{
    public DeckListView()
    {
        InitializeComponent();
        
        DeckListViewModel vm = Program.ServiceProvider.GetRequiredService<DeckListViewModel>();
        
        this.Find<AutoCompleteBox>("CardSearchBox").AsyncPopulator = vm.UpdateSearchList;
    }
}