FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . ./VerificationService/
COPY ../Shared.Infrastructure ./Shared.Infrastructure/
WORKDIR /src/VerificationService
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 80
ENTRYPOINT ["dotnet", "VerificationService.dll"]