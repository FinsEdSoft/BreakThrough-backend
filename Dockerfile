# Stage 1: build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj from backend folder
COPY Breakthrough.Backend/*.csproj Breakthrough.Backend/
WORKDIR /src/Breakthrough.Backend
RUN dotnet restore

# Copy everything else from backend
COPY Breakthrough.Backend/. .
RUN dotnet publish -c Release -o /app

# Stage 2: runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:10000
ENTRYPOINT ["dotnet", "Breakthrough.Backend.dll"]
