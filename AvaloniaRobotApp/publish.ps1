# Publish script for AvaloniaRobotApp

$platforms = @("win-x64", "osx-x64", "linux-x64")
$project = "AvaloniaRobotApp.csproj"

foreach ($rid in $platforms) {
    Write-Host "Publishing for $rid..." -ForegroundColor Cyan
    dotnet publish $project -c Release -r $rid --self-contained true -p:PublishSingleFile=true -o "./publish/$rid"
}

Write-Host "Publish complete! Check the ./publish folder." -ForegroundColor Green
