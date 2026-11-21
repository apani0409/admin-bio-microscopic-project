FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish Backend.Api/UCR.EB.BioMicroscopeAdmin.Backend.Api.csproj \
    --configuration Release --output /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Las carpetas se crean aquí para que los volúmenes hereden el dueño sin privilegios.
RUN mkdir -p /data/images /data/keys /app/wwwroot \
    && chown -R app:app /data /app/wwwroot

USER app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "UCR.EB.BioMicroscopeAdmin.Backend.Api.dll"]
