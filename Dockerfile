# Stage 1: build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore
COPY Breakthrough.Backend/*.csproj ./Breakthrough.Backend/
RUN dotnet restore Breakthrough.Backend/Breakthrough.Backend.csproj

# Copy the rest of the code
COPY . .

# Publish directly from the project file
RUN dotnet publish Breakthrough.Backend/Breakthrough.Backend.csproj -c Release -o /app

# Stage 2: runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:10000

ENTRYPOINT ["dotnet", "Breakthrough.Backend.dll"]
