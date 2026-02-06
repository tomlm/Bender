dotnet tool uninstall -g PrettyStick.Console
dotnet tool uninstall -g PrettyStick
dotnet build -c Release
dotnet pack -c Release 
dotnet tool install -g PrettyStick --source nupkg