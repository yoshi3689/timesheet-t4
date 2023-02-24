FROM mcr.microsoft.com/dotnet/sdk:7.0
RUN dotnet restore
RUN dotnet-ef migrations add M1 -o Data/Migrations
RUN dotnet-ef database update
RUN dotnet publish-o dist
COPY dist /app
WORKDIR /app
ENV ASPNETCORE_URLS http://+:80
EXPOSE 80/tcp
EXPOSE 443/tcp
ENTRYPOINT ["dotnet", "TimesheetApp.dll"]
