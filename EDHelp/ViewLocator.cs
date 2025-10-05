using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Dock.Model.Core;
using EDHelp.ViewModels;

namespace EDHelp;

public class ViewLocator : IDataTemplate
{
    private readonly IServiceProvider _provider;

    public ViewLocator(IServiceProvider provider)
    {
        _provider = provider;
    }

    private Control? Resolve(object viewModel)
    {
        var vmType = viewModel.GetType();
        
        var viewName = vmType.FullName?.Replace("ViewModel", "View");
        if (viewName is null)
            return null;

        var viewType = Type.GetType(viewName);
        if (viewType != null && _provider.GetService(viewType) is Control view)
        {
            view.DataContext = viewModel;
            return view;
        }

        return null;
    }

    public Control? Build(object? data)
    {
        if (data is null)
            return null;

        if (Resolve(data) is Control control)
        {
            return control;
        }

        var viewName = data.GetType().FullName?.Replace("ViewModel", "View");
        return new TextBlock { Text = $"Not Found: {viewName}" };
    }

    public bool Match(object? data)
    {
        if (data is null)
        {
            return false;
        }

        return data is IDockable || Resolve(data) is not null;
    }
}