# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY EdgegapProxy.csproj .
RUN dotnet restore "EdgegapProxy.csproj"

# Copy source code and build
COPY . .
RUN dotnet build "EdgegapProxy.csproj" -c Release -o /app/build
RUN dotnet publish "EdgegapProxy.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Copy published app from build stage
COPY --from=build /app/publish .

# Render.com provides PORT environment variable (default: 10000)
# ASP.NET Core MUST bind to 0.0.0.0:$PORT (not localhost!)
# This allows external connections from Render's routing layer
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT:-10000}

# Expose port (dynamic from environment)
EXPOSE ${PORT:-10000}

# Start the application
ENTRYPOINT ["dotnet", "EdgegapProxy.dll"]
