@echo on
chcp 1251 > nul
set wd=%~dp0
pushd "%wd%"

cscript.exe //nologo ADDMIMETYPES.VBS  .xap  application/x-silverlight-app
cscript.exe //nologo ADDMIMETYPES.VBS  .xaml application/xaml+xml
cscript.exe //nologo ADDMIMETYPES.VBS  .thmx application/x-ms-powerpoint

pause
exit
