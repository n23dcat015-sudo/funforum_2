```bat
@echo off
title Funforum Launcher

echo =================================
echo       Starting Funforum
echo =================================

start "Funforum Backend" /D "%~dp0backend\Funforum.Api" dotnet run

start "Funforum Frontend" /D "%~dp0frontend" npm run dev

exit
```
