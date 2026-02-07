using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Bender.ViewModels;
using Bender.Views;

namespace Bender.Desktop;

public partial class App : Bender.App
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

}