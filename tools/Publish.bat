@ECHO OFF
SET version=1.0.0.0
"C:\Program Files (x86)\NuGet\nuget.exe" push NLog.Targets.ElasticSearch.%version%.nupkg
pause