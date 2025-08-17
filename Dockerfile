# Stage 1: build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy everything and restore
COPY . .
RUN dotnet restore

# Publish app to /app folder
RUN dotnet publish -c Release -o /app

# Stage 2: runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Copy published output from build stage
COPY --from=build /app .

# Expose port for Render (it maps internally)
ENV ASPNETCORE_URLS=http://+:10000

# Start your API
ENTRYPOINT ["dotnet", "Breakthrough.Backend.dll"]
