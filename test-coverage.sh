#!/usr/bin/env bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
reportgenerator -reports:*/*cobertura.xml -targetdir:./coverage-report