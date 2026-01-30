using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Blender.ViewModels;
using Blender.Views;

namespace Blender.Console;

public partial class App : Blender.App
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

}