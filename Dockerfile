FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build-env
WORKDIR /App

# Copy everything
COPY . ./
# Restore as distinct layers
RUN dotnet restore
# Build and publish a release
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:7.0

# Update package index and install default-mysql-client
RUN apt-get update && \
    apt-get install -y default-mysql-client

# Add the directory containing mysqldump to the PATH environment variable
ENV PATH="/usr/bin:${PATH}"

WORKDIR /App
COPY --from=build-env /App/out .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://*:8080
ENTRYPOINT ["dotnet", "TimesheetApp.dll"]
