FROM mcr.microsoft.com/dotnet/aspnet:3.1-alpine

WORKDIR /app

COPY ./PublishOutput ./

ENTRYPOINT ["dotnet", "CountTiffs.dll"]


