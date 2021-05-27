@echo off
setlocal EnableDelayedExpansion

set /P revversion=<version.txt

if "!revversion!"=="" (
    set /P revversion=1
) else (
    set /A revversion=revversion+1
)

xcopy /Y /F Dockerfile ..\bin\Release


echo building labizbille/counttiff:1.0.%revversion%
echo %revversion% > version.txt
docker build -t labizbille/counttiff:1.0.%revversion% ../bin/Release

echo ready to publish labizbille/counttiff:1.0.%revversion%, Press CTRL-C to exit or any key to continue

pause
docker push labizbille/counttiff:1.0.%revversion%

echo all done
pause 