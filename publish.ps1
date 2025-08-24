# 极简：当前目录必须有 .sln；需要已配置 $env:NUGET_API_KEY
$ErrorActionPreference = 'Stop'

# 1) 找到当前目录第一个 .sln
$sln = (Get-ChildItem -Filter *.sln | Select-Object -First 1).FullName
if (-not $sln) { throw "当前目录没有 .sln 文件。" }

# 2) 打包到 ./artifacts（含符号包和 SourceLink 最佳实践）
$outDir = Join-Path (Get-Location) "artifacts"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

dotnet pack $sln -c Release -o $outDir `
  /p:IncludeSymbols=true /p:SymbolPackageFormat=snupkg `
  /p:ContinuousIntegrationBuild=true

# 3) 推送到 nuget.org（需要环境变量 NUGET_API_KEY）
if (-not $env:NUGET_API_KEY) { throw "请先设置环境变量 NUGET_API_KEY（NuGet API Key）。" }

Get-ChildItem $outDir -Filter *.nupkg | ForEach-Object {
  dotnet nuget push $_.FullName `
    --api-key $env:NUGET_API_KEY `
    --source https://api.nuget.org/v3/index.json `
    --skip-duplicate
}

Get-ChildItem $outDir -Filter *.snupkg | ForEach-Object {
  dotnet nuget push $_.FullName `
    --api-key $env:NUGET_API_KEY `
    --source https://api.nuget.org/v3/index.json `
    --skip-duplicate
}

Write-Host "`n✅ 发布完成。产物目录：$outDir" -ForegroundColor Green
