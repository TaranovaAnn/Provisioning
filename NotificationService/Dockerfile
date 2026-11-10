FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Shared.Contracts/Shared.Contracts.csproj Shared.Contracts/
COPY Shared.Observability/Shared.Observability.csproj Shared.Observability/
COPY NotificationService/NotificationService.csproj NotificationService/
RUN dotnet restore NotificationService/NotificationService.csproj
COPY Shared.Contracts/ Shared.Contracts/
COPY Shared.Observability/ Shared.Observability/
COPY NotificationService/ NotificationService/
RUN dotnet publish NotificationService/NotificationService.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "NotificationService.dll"]
