FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

COPY *.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --chown=app:app --from=build /out .
USER app

EXPOSE 9000
EXPOSE 9001

ENTRYPOINT ["dotnet", "TcpUdpGateway.dll"]