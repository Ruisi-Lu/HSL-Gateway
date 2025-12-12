FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["HslGateway/HslGateway.csproj", "HslGateway/"]
RUN dotnet restore "HslGateway/HslGateway.csproj"
COPY . .
WORKDIR "/src/HslGateway"
RUN dotnet publish "HslGateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 50051
ENTRYPOINT ["dotnet", "HslGateway.dll"]
