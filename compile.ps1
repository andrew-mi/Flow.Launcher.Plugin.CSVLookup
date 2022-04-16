function Invoke-ActualReleaseCompile {
    param ([Parameter(Mandatory)]$userPluginFolder)
    dotnet publish Flow.Launcher.Plugin.CSVLookup -c Release -r win-x64
    Compress-Archive -LiteralPath Flow.Launcher.Plugin.CSVLookup/bin/Release/win-x64/publish -DestinationPath Flow.Launcher.Plugin.CSVLookup/bin/CSVLookup.zip -Force
    Copy-Item -Recurse Flow.Launcher.Plugin.CSVLookup\bin\Release\win-x64\publish\* $userPluginFolder
}

function Invoke-DebugCompile {
    param ([Parameter(Mandatory)]$userPluginFolder)
    dotnet publish Flow.Launcher.Plugin.CSVLookup -c Debug -r win-x64
    Copy-Item -Recurse Flow.Launcher.Plugin.CSVLookup\bin\Debug\win-x64\publish\* $userPluginFolder
}


function Invoke-Compile {
    Stop-Process -Name "Flow.Launcher"
    Start-Sleep -Seconds 3
    Invoke-DebugCompile -userPluginFolder "C:\Users\andre\OneDrive\Utility\FlowLauncher\app-1.9.3\UserData\Plugins\CSV Lookup-0.0.1"
    Start-Process -FilePath "C:\Users\andre\OneDrive\Utility\FlowLauncher\app-1.9.3\Flow.Launcher.exe"
    Write-Host "You might need to enable the plugin in the launcher"
}