@echo off
setlocal

set AppPath=%~dp0glTF.exe

title glTF Shell Extensions - Setup

echo Registering system file association (.glb)...
reg add HKCR\SystemFileAssociations\.glb\Shell\Unpack /ve /d "&Unpack to glTF..." /f
reg add HKCR\SystemFileAssociations\.glb\Shell\Unpack\command /ve /d "\"%AppPath%\" Unpack \"%%1\"" /f
echo.

echo Registering system file association (.gltf)...
reg add HKCR\SystemFileAssociations\.gltf\Shell\Pack /ve /d "&Pack to Binary glTF..." /f
reg add HKCR\SystemFileAssociations\.gltf\Shell\Pack\command /ve /d "\"%AppPath%\" Pack \"%%1\"" /f
echo.

echo Press any key to exit...
timeout 3 >nul
