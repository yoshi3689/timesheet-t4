FROM mcr.microsoft.com/dotnet/sdk:7.0
RUN dotnet publish -o dist
COPY dist /app
WORKDIR /app
ENV ASPNETCORE_URLS http://+:80
EXPOSE 80/tcp
EXPOSE 443/tcp
ENTRYPOINT ["dotnet", "TimesheetApp.dll"]
