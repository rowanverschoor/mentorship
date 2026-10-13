FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
# Copy the project file first so the restore step is cached
COPY WeatherApi/WeatherApi.csproj WeatherApi/
RUN dotnet restore WeatherApi/WeatherApi.csproj
COPY WeatherApi/ WeatherApi/
RUN dotnet publish WeatherApi/WeatherApi.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
# .NET 8 images listen on 8080 by default
EXPOSE 8080
ENTRYPOINT ["dotnet", "WeatherApi.dll"]
