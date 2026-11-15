The build workflow zips the published WinDownloader app into `app.zip` here before compiling Win Setup,
so the installer carries the app inside it. The zip is not committed.

To do it locally (PowerShell):

```powershell
dotnet publish src/WinDownloader/WinDownloader.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o out/app
Compress-Archive -Path out/app/* -DestinationPath src/WinSetup/Payload/app.zip -Force
dotnet publish src/WinSetup/WinSetup.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o out/setup
```
