# ============================================================
# Dockerfile da API do Servia (F0.1)
#
# Build em dois estágios: o SDK compila, a imagem final leva apenas o
# runtime. Isso reduz o tamanho e a superfície de ataque da imagem.
#
# Uso:
#   docker build -f src/Servia.Api/Dockerfile -t servia-api .
# ============================================================

# ---------- Estágio de build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia apenas os arquivos de projeto primeiro: o cache de restore é
# reaproveitado enquanto as dependências não mudarem.
COPY ["Servia.Api/Servia.Api.csproj", "Servia.Api/"]
COPY ["Servia.Application/Servia.Application.csproj", "Servia.Application/"]
COPY ["Servia.Domain/Servia.Domain.csproj", "Servia.Domain/"]
COPY ["Servia.Infrastructure/Servia.Infrastructure.csproj", "Servia.Infrastructure/"]

RUN dotnet restore "Servia.Api/Servia.Api.csproj"

# Copia o restante do código e publica.
COPY . .
WORKDIR "/src/Servia.Api"
RUN dotnet publish "Servia.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ---------- Estágio de runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Executa como usuário sem privilégios.
USER $APP_UID

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Servia.Api.dll"]
