# Despliegue Docker — entorno TEST integrado

Este despliegue conecta `gesfactu-api-test` con:

- el backend Python de la aplicación mediante una red Docker interna;
- una instancia PostgreSQL ya existente, usando base y usuario propios;
- el endpoint real **AEAT TEST** mediante mTLS.

No crea un contenedor PostgreSQL nuevo y no publica la API .NET a Internet.

## 1. Prerrequisitos

En la VM deben existir:

1. Docker Engine + Docker Compose.
2. El contenedor PostgreSQL existente.
3. Una red Docker compartida por Python, PostgreSQL y gesFactu.
4. El certificado AEAT exportado como PFX/P12 con clave privada.
5. Una base y usuario PostgreSQL exclusivos para esta API.

Comprobar redes:

```bash
docker network ls
docker inspect <contenedor-postgres> --format '{{json .NetworkSettings.Networks}}'
docker inspect <contenedor-python> --format '{{json .NetworkSettings.Networks}}'
```

Si PostgreSQL todavía no está conectado a la red interna elegida:

```bash
docker network connect <red-interna> <contenedor-postgres>
```

## 2. Crear base y usuario propios

Entrar al PostgreSQL existente con un rol administrador:

```bash
docker exec -it <contenedor-postgres> psql -U postgres
```

Ejemplo:

```sql
CREATE USER gesfactu_verifactu_test
WITH PASSWORD 'PASSWORD_LARGA_Y_ALEATORIA';

CREATE DATABASE gesfactu_verifactu_test
OWNER gesfactu_verifactu_test;
```

La API no debe compartir base ni usuario con Python.

## 3. Configuración local

```bash
cp .env.test.example .env.test
chmod 600 .env.test
```

Editar `.env.test` con los nombres reales de red/PostgreSQL y credenciales.

Crear directorio de secretos:

```bash
sudo install -d -m 700 /opt/gesfactu-test/secrets
```

Crear dos claves distintas de al menos 32 bytes:

```bash
openssl rand -base64 48 | sudo tee /opt/gesfactu-test/secrets/api-key.txt >/dev/null
openssl rand -base64 48 | sudo tee /opt/gesfactu-test/secrets/admin-key.txt >/dev/null
sudo chmod 600 /opt/gesfactu-test/secrets/*.txt
```

Copiar el PFX AEAT TEST y crear el fichero que contiene únicamente su contraseña:

```text
/opt/gesfactu-test/secrets/aeat-test.pfx
/opt/gesfactu-test/secrets/aeat-pfx-password.txt
```

Nunca versionar esos ficheros.

## 4. Aplicar migraciones EF

Desde la raíz del repositorio en la VM:

```bash
set -a
. ./.env.test
set +a

docker run --rm \
  --network "$GESFACTU_TEST_DOCKER_NETWORK" \
  -v "$PWD:/src" \
  -w /src \
  -e ConnectionStrings__DefaultConnection="Host=$GESFACTU_TEST_DB_HOST;Port=$GESFACTU_TEST_DB_PORT;Database=$GESFACTU_TEST_DB_NAME;Username=$GESFACTU_TEST_DB_USER;Password=$GESFACTU_TEST_DB_PASSWORD" \
  mcr.microsoft.com/dotnet/sdk:8.0 \
  sh -c 'dotnet tool restore && dotnet restore src/Api/gesFactu.Api/gesFactu.Api.csproj && dotnet ef database update --project src/Infrastructure/gesFactu.Infrastructure.csproj --startup-project src/Api/gesFactu.Api/gesFactu.Api.csproj'
```

Después debe existir `__EFMigrationsHistory` y las tablas de gesFactu en
`gesfactu_verifactu_test`.

## 5. Levantar la API

```bash
docker compose --env-file .env.test -f docker-compose.test.yml up -d --build
```

Comprobar:

```bash
docker ps
docker logs --tail 200 gesfactu-api-test
```

## 6. Health checks

Como la API no publica puerto al host, comprobar desde la propia red Docker:

```bash
docker run --rm \
  --network "$GESFACTU_TEST_DOCKER_NETWORK" \
  curlimages/curl:latest \
  http://gesfactu-api-test:8080/health/live
```

Y readiness:

```bash
docker run --rm \
  --network "$GESFACTU_TEST_DOCKER_NETWORK" \
  curlimages/curl:latest \
  http://gesfactu-api-test:8080/health/ready
```

`/health/ready` valida conexión PostgreSQL y carga real del certificado.

## 7. Llamada desde Python

Dentro de la misma red Docker:

```text
http://gesfactu-api-test:8080
```

Las operaciones de API requieren:

```text
X-GesFactu-Api-Key: <clave>
Idempotency-Key: <uuid estable para la operación>
```

Los endpoints administrativos requieren además `X-GesFactu-Admin-Key`.

## 8. Cerrojo AEAT

El compose TEST fija deliberadamente:

```text
ASPNETCORE_ENVIRONMENT=Staging
VeriFactu__Environment=Test
VeriFactu__AllowProduction=false
VeriFactu__ClientMode=SoapClient
```

Por tanto, este despliegue resuelve el endpoint oficial:

```text
https://prewww1.aeat.es/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP
```

y no el endpoint de Producción.
