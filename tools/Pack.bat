@ECHO OFF 
SET version=0.9.0.0
SET out=.
"C:\Program Files (x86)\NuGet\nuget.exe" pack ..\src\NLog.Targets.ElasticSearch\NLog.Targets.ElasticSearch.csproj -Symbols -Prop Configuration=Release -OutputDirectory %out% -Version %version%
pause