# ================================
# 1. Build Stage
# ================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution file
COPY BakeFix.sln ./

# Copy project folder
COPY BakeFix/ BakeFix/

# Restore
RUN dotnet restore BakeFix/BakeFix.csproj

# Build + publish
RUN dotnet publish BakeFix/BakeFix.csproj -c Release -o /app/publish


# ================================
# 2. Runtime Stage
# ================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Copy from build stage
COPY --from=build /app/publish .

# Render uses $PORT
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT}

# Expose container port (optional)
EXPOSE 8080

ENTRYPOINT ["dotnet", "BakeFix.dll"]