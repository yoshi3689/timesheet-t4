FROM mcr.microsoft.com/dotnet/sdk:7.0 as build-env
COPY *.csproj .
RUN dotnet restore
COPY src .
RUN dotnet publish -c Release -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:7.0 as runtime
WORKDIR /publish
COPY --from=build-env /publish .
EXPOSE 80/tcp
EXPOSE 443/tcp
ENTRYPOINT ["dotnet", "TimesheetApp.dll"]
