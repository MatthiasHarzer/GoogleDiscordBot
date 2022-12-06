FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /app

COPY . ./

# Restore as distinct layers
RUN dotnet restore

# Build and publish a release
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /app
COPY --from=build-env /app/out .

ENV DiscordToken=NjMwNTAyMTMyNzcyMTEwMzQ4.XZpO3g.RyvGku6P3U4q_IwaNlgTUMj6aig
ENV GoogleApiKey=AIzaSyBfr7Cp04M3htxqO-6ihv9K7qO3NBg9kCU
ENV SearchEngineID=004710594889092874825:eqlgi1njajn

ENTRYPOINT ["dotnet", "GoogleBot.dll"]