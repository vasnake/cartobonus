@rem -*- mode: bat; coding: cp1251 -*-

goto MWBDEPLOY
goto TEST

:MWBDEPLOY

@echo off
chcp 1251 > nul
set wd=%~dp0
pushd "%wd%"
set NLS_LANG=AMERICAN_CIS.UTF8
set PYTHONPATH=
set proxy=http://proxy.algis.com:3128
set http_proxy=%proxy%
set ftp_proxy=%proxy%
set all_proxy=%proxy%
@cls

REM ~ clear ie cache
rundll32.exe inetcpl.cpl,ClearMyTracksByProcess 8

set RLOG=c:\t\robo.log
@REM ~ set devroot=c:\d\code\sl.esri.mapservices.dir
set devroot=c:\d\code\git\silverlight\mapservicesdir

set from=%devroot%\maps.web.builder\mwb02\mwb02.AddIns\Bin\Release
for %%v in (c:\Inetpub\wwwroot\Builder\Extensions c:\Inetpub\wwwroot\Apps\app5\Extensions) do (
	robocopy "%from%" "%%v" *.xap /LEV:1 /B /COPY:DT /R:1 /W:1 /LOG+:%RLOG% /TEE /NP
)

set from=%devroot%\QueryRelatedRecords\QueryRelatedRecords\QueryRelatedRecords.AddIns\Bin\Release
for %%v in (c:\Inetpub\wwwroot\Builder\Extensions c:\Inetpub\wwwroot\Apps\app5\Extensions) do (
	robocopy "%from%" "%%v" *.xap /LEV:1 /B /COPY:DT /R:1 /W:1 /LOG+:%RLOG% /TEE /NP
)

set from=%devroot%\Ice\Ice\Ice.AddIns\Bin\Release
for %%v in (c:\Inetpub\wwwroot\Builder\Extensions c:\Inetpub\wwwroot\Apps\app5\Extensions) do (
	robocopy "%from%" "%%v" *.xap /LEV:1 /B /COPY:DT /R:1 /W:1 /LOG+:%RLOG% /TEE /NP
)

set from=%devroot%\agsproxy
for %%v in (c:\Inetpub\wwwroot\agsproxy) do (
	robocopy "%from%" "%%v" *.ashx *.config /LEV:1 /B /COPY:DT /R:1 /W:1 /LOG+:%RLOG% /TEE /NP
)

set from=%devroot%\kvsproxy
set to=c:\Inetpub\wwwroot\kvsproxy
robocopy "%from%" "%to%" /E /B /COPY:DT /R:1 /W:1 /LOG+:%RLOG% /TEE /NP /FFT /PURGE

@REM ~ clear ie cache
rmdir /S /Q "c:\Documents and Settings\boss.VDESK\Local Settings\Temporary Internet Files"


set from=%devroot%\Products.VKeyValObj
set to=c:\d\Zope\zope213\src\Products.VKeyValObj
robocopy "%from%" "%to%" /E /B /COPY:DT /R:1 /W:1 /LOG+:%RLOG% /TEE /NP /FFT /PURGE

popd
exit


:TEST
@echo off
chcp 1251 > nul
set wd=%~dp0
pushd "%wd%"
set NLS_LANG=AMERICAN_CIS.UTF8
set PYTHONPATH=
set proxy=http://proxy.algis.com:3128
set http_proxy=%proxy%
set ftp_proxy=%proxy%
set all_proxy=%proxy%
@cls
for %%v in (c:\Inetpub\wwwroot\Builder\Extensions c:\Inetpub\wwwroot\Apps\app1\Extensions) do (
	@echo v=[%%v]
)
help for
exit
