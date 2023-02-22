FROM mcr.microsoft.com/dotnet/aspnet:7.0
COPY dist /app
WORKDIR /app
ENV ASPNETCORE_URLS http://+:80
EXPOSE 80/tcp
EXPOSE 443/tcp
ENTRYPOINT ["dotnet", "TimesheetApp.dll"]
