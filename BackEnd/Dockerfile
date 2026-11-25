FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY EMG.slnx .
COPY src/EMG.Domain/EMG.Domain.csproj src/EMG.Domain/
COPY src/EMG.Shared/EMG.Shared.csproj src/EMG.Shared/
COPY src/EMG.Infrastructure/EMG.Infrastructure.csproj src/EMG.Infrastructure/
COPY src/EMG.WebApi/EMG.WebApi.csproj src/EMG.WebApi/
RUN dotnet restore src/EMG.WebApi/EMG.WebApi.csproj
COPY . .
RUN dotnet publish src/EMG.WebApi/EMG.WebApi.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /app/uploads /app/data-protection-keys \
    && chmod 700 /app/uploads /app/data-protection-keys
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "EMG.WebApi.dll"]
