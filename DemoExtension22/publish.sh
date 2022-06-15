cd ../../SmartPendantSDK/csharp/ || exit
msbuild SDK.csproj /t:build
cd ../../DemoExtension22/ || exit
dotnet publish -r linux-arm --self-contained true
