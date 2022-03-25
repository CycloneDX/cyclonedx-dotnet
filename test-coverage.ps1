dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
reportgenerator -reports:$(ls -*/*cobertura.xml | %{ $_.FullName}) -targetdir:./coverage-report