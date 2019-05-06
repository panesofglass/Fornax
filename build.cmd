@echo off
cls
dotnet restore build.proj
if not "%*"=="" (
    dotnet fake build --target %*
) else (
    dotnet fake build
)