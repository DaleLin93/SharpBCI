@echo off
set projectDir=%1
set projectDir=%projectDir:~1,-1%
set outDir=%2
set outDir=%outDir:~1,-1%
set configurationName=%3
set sourceDir=%projectDir%%outDir%
set targetDir=%~dp0%outDir%
echo %sourceDir%
if not exist "%targetDir%" (
  md "%targetDir%"
)
xcopy "%sourceDir%*" "%targetDir%" /C /Y
if %configurationName%==Release (
	del "%targetDir%*.pdb" /Q
)