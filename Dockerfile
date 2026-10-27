FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/PersonalAccountingApi/PersonalAccountingApi.csproj src/PersonalAccountingApi/
RUN dotnet add src/PersonalAccountingApi package Microsoft.EntityFrameworkCore
RUN dotnet restore src/PersonalAccountingApi
COPY src/ src/
RUN dotnet publish src/PersonalAccountingApi -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
# Non-root user built into the base image; listens on 8080.
USER $APP_UID
ENTRYPOINT ["dotnet", "PersonalAccountingApi.dll"]
