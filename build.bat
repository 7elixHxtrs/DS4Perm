@echo off
where dotnet >nul 2>nul || (echo Instala el .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0 & pause & exit /b 1)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o out
echo.
echo Listo: out\DS4Perm.exe
pause
