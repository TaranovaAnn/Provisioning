FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Shared.Contracts/Shared.Contracts.csproj Shared.Contracts/
COPY Shared.Observability/Shared.Observability.csproj Shared.Observability/
COPY RequestService/RequestService.csproj RequestService/
RUN dotnet restore RequestService/RequestService.csproj
COPY Shared.Contracts/ Shared.Contracts/
COPY Shared.Observability/ Shared.Observability/
COPY RequestService/ RequestService/
RUN dotnet publish RequestService/RequestService.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "RequestService.dll"]
