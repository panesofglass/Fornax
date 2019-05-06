#!/bin/bash
dotnet restore build.proj
if test "%*" = ""
then
    dotnet fake build
else
    dotnet fake build --target %*
fi