using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DeckDesigner.UI.Controls;
using DeckDesigner.UI.ViewModels;

namespace DeckDesigner.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnFitClick(object? sender, RoutedEventArgs e)
    {
        this.FindControl<DeckCanvas>("Canvas")?.RequestFit();
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        (DataContext as MainViewModel)?.DeleteSelected();
    }
}
