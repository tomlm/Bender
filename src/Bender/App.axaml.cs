using Avalonia.Markup.Xaml;

namespace Bender.Console;

public partial class App : Bender.App
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

}