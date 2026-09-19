# LA CABRERA - Guia de Deploy

## Requisitos

- Docker 24+
- Docker Compose 2+
- SQL Server (externo o contenedor)
- 4GB RAM minimo

## Arquitectura de Deploy

```
                    ┌─────────────┐
                    │   Nginx     │ :80
                    │  (Frontend) │
                    └──────┬──────┘
                           │
                    ┌──────┴──────┐
                    │             │
              ┌─────┴─────┐ ┌─────┴─────┐
              │  API      │ │  Static   │
              │  Proxy    │ │  Files    │
              └─────┬─────┘ └───────────┘
                    │
             ┌──────┴──────┐
             │  .NET API   │ :8080
             │  (Backend)  │
             └──────┬──────┘
                    │
             ┌──────┴──────┐
             │ SQL Server  │ :1433
             │  (Externo)  │
             └─────────────┘
```

## Opcion 1: Demo Local (con SQL Server en contenedor)

### 1. Configurar variables de entorno

```bash
cp .env.example .env
# Editar .env si es necesario
```

### 2. Levantar todo el stack

```bash
docker-compose up -d
```

### 3. Crear la base de datos

**Importante**: Los scripts SQL deben ejecutarse manualmente.

```bash
# Esperar que SQL Server este listo (~30 segundos)
docker-compose logs sqlserver

# Conectar al contenedor SQL Server
docker exec -it lacabrera-sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'YourStrong@Passw0rd' -C

# O usar SQL Server Management Studio (SSMS):
# Server: localhost,1433
# User: sa
# Password: YourStrong@Passw0rd
```

Ejecutar los scripts en orden:
1. `database/scripts/00_create_database.sql`
2. `database/scripts/01_create_schemas.sql`
3. `database/scripts/02_create_tables.sql`
4. `database/scripts/03_create_constraints_indexes.sql`
5. `database/scripts/04_create_views_dashboard.sql`
6. `database/scripts/05_create_stored_procedures_ingestion.sql`
7. `database/scripts/06_seed_demo_data.sql`
8. `database/scripts/06b_seed_extended_products.sql`
9. `database/scripts/07_seed_realistic_sales_data.sql`

### 4. Verificar servicios

```bash
# Estado de contenedores
docker-compose ps

# Health check API
curl http://localhost:5000/api/health

# Abrir frontend
open http://localhost:3000  # Mac
xdg-open http://localhost:3000  # Linux
start http://localhost:3000  # Windows
```

## Opcion 2: Produccion (SQL Server externo)

### 1. Configurar SQL Server externo

Ejecutar los scripts SQL en el servidor externo:

```bash
sqlcmd -S tu-servidor.database.windows.net -U admin -P TuPassword \
  -i database/scripts/00_create_database.sql
# ... repetir para cada script
```

### 2. Configurar variables de entorno

```bash
cp .env.example .env
```

Editar `.env`:
```env
DB_CONNECTION_STRING=Server=tu-servidor.database.windows.net;Database=LaCabreraDB;User Id=admin;Password=TuPassword;TrustServerCertificate=True
ASPNETCORE_ENVIRONMENT=Production
```

### 3. Deploy con compose de produccion

```bash
docker-compose -f docker-compose.prod.yml up -d
```

## Comandos Utiles

### Levantar servicios

```bash
# Demo (con SQL Server)
docker-compose up -d

# Produccion (SQL externo)
docker-compose -f docker-compose.prod.yml up -d

# Rebuild tras cambios
docker-compose up -d --build
```

### Bajar servicios

```bash
# Parar sin eliminar datos
docker-compose down

# Parar y eliminar volumenes (CUIDADO: borra datos)
docker-compose down -v
```

### Ver logs

```bash
# Todos los servicios
docker-compose logs -f

# Solo API
docker-compose logs -f api

# Solo frontend
docker-compose logs -f frontend

# Solo SQL Server
docker-compose logs -f sqlserver
```

### Estado y diagnostico

```bash
# Estado de contenedores
docker-compose ps

# Recursos usados
docker stats

# Health check
curl http://localhost:5000/api/health
curl http://localhost:3000/health
```

### Reiniciar servicios

```bash
# Reiniciar todo
docker-compose restart

# Reiniciar solo API
docker-compose restart api

# Recrear un servicio
docker-compose up -d --force-recreate api
```

### Limpiar

```bash
# Eliminar contenedores parados
docker container prune

# Eliminar imagenes no usadas
docker image prune

# Limpieza completa
docker system prune -a
```

## Puertos

| Servicio | Puerto Local | Puerto Contenedor |
|----------|-------------|-------------------|
| Frontend | 3000 (demo) / 80 (prod) | 80 |
| API | 5000 | 8080 |
| SQL Server | 1433 | 1433 |

## Endpoints

| Endpoint | Descripcion |
|----------|-------------|
| `GET /api/health` | Health check de la API |
| `GET /api/v1/dashboard/home` | Dashboard principal |
| `POST /api/v1/sales/daily-batch` | Ingesta de ventas |
| `GET /swagger` | Documentacion API (solo dev) |

## Configuracion por Ambiente

### Development

```yaml
environment:
  - ASPNETCORE_ENVIRONMENT=Development
  - Serilog__MinimumLevel__Default=Debug
```

- Swagger habilitado
- Logs detallados
- CORS permisivo

### Production

```yaml
environment:
  - ASPNETCORE_ENVIRONMENT=Production
  - Serilog__MinimumLevel__Default=Warning
```

- Swagger deshabilitado por defecto
- Logs solo warnings y errores
- CORS restrictivo

### Habilitar Swagger en Produccion (temporal)

Modificar `Program.cs`:
```csharp
// Descomentar estas lineas en produccion si necesitas Swagger
// app.UseSwagger();
// app.UseSwaggerUI();
```

## Troubleshooting

### API no conecta a SQL Server

```bash
# Verificar que SQL Server esta healthy
docker-compose ps
docker-compose logs sqlserver

# Verificar connection string
docker-compose exec api env | grep Connection

# Probar conexion desde el contenedor API
docker-compose exec api apt-get update && apt-get install -y curl
docker-compose exec api curl telnet://sqlserver:1433
```

### Frontend no carga datos

```bash
# Verificar proxy nginx
docker-compose logs frontend

# Probar API directamente
curl http://localhost:5000/api/v1/dashboard/home

# Verificar que el frontend puede alcanzar el API
docker-compose exec frontend wget -O- http://api:8080/api/health
```

### Base de datos vacia

Los scripts SQL deben ejecutarse manualmente. Ver seccion "Crear la base de datos".

### Out of Memory

Aumentar recursos de Docker:
- Docker Desktop > Settings > Resources > Memory: 4GB minimo

## Backup y Restore

### Backup de SQL Server

```bash
docker exec lacabrera-sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'YourStrong@Passw0rd' -C \
  -Q "BACKUP DATABASE [LaCabreraDB] TO DISK = '/var/opt/mssql/backup/LaCabreraDB.bak'"

# Copiar backup fuera del contenedor
docker cp lacabrera-sqlserver:/var/opt/mssql/backup/LaCabreraDB.bak ./backups/
```

### Restore

```bash
# Copiar backup al contenedor
docker cp ./backups/LaCabreraDB.bak lacabrera-sqlserver:/var/opt/mssql/backup/

# Restore
docker exec lacabrera-sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'YourStrong@Passw0rd' -C \
  -Q "RESTORE DATABASE [LaCabreraDB] FROM DISK = '/var/opt/mssql/backup/LaCabreraDB.bak'"
```

## Deploy Automatizado a Produccion

### Requisitos previos

1. **SSH Key configurada**: El script usa una clave SSH para conectarse a produccion
   ```powershell
   # Generar clave (solo una vez)
   & "C:\Program Files\Git\usr\bin\ssh-keygen.exe" -t ed25519 -f "$env:USERPROFILE\.ssh\id_ed25519_lacabrera" -N ""
   ```

2. **Agregar clave al servidor**: Copiar la clave publica al servidor de produccion
   ```powershell
   Get-Content "$env:USERPROFILE\.ssh\id_ed25519_lacabrera.pub"
   # Agregar el contenido a /root/.ssh/authorized_keys en el servidor
   ```

### Uso del script de deploy

```powershell
# Deploy completo (valida builds, commit, push, actualiza produccion)
.\deploy\deploy.bat

# Deploy con mensaje de commit personalizado
.\deploy\deploy.bat "Fix: correccion de bug en dashboard"

# Deploy sin hacer commit (usa lo que ya esta en GitHub)
.\deploy\deploy.bat --skip-commit

# Deploy sin validar builds locales (mas rapido)
.\deploy\deploy.bat --skip-build
```

### Que hace el script

1. **Valida builds locales**: Compila frontend y backend para detectar errores
2. **Commit y Push**: Sube cambios a GitHub
3. **SSH a Produccion**: Conecta al servidor `200.58.127.114:5794`
4. **Git Pull**: Actualiza codigo en `/opt/lacabrera`
5. **Docker Rebuild**: Reconstruye imagenes de frontend y API
6. **Restart Services**: Reinicia containers y nginx (SSL)
7. **Health Checks**: Verifica que todo funcione

### Arquitectura de Produccion

```
Internet
    │
    ▼ (HTTPS :443)
┌─────────────────────────────────────┐
│         Nginx (SSL Proxy)           │
│   /etc/nginx/sites-enabled/cabreraapp
└─────────────┬───────────────────────┘
              │
    ┌─────────┴─────────┐
    │                   │
    ▼ (:3000)           ▼ (:5000)
┌───────────┐      ┌───────────┐
│  Docker   │      │  Docker   │
│ Frontend  │      │    API    │
│  (Nginx)  │      │  (.NET)   │
└───────────┘      └─────┬─────┘
                         │
                         ▼ (:1433)
                   ┌───────────┐
                   │  Docker   │
                   │ SQL Server│
                   └───────────┘
```

### URLs de Produccion

| Servicio | URL |
|----------|-----|
| Frontend | https://cabreraapp.pillow.com.ar |
| API Health | https://cabreraapp.pillow.com.ar/api/health |
| Login | https://cabreraapp.pillow.com.ar/login |

### Troubleshooting Deploy

**SSH no conecta:**
```powershell
# Verificar clave
Test-Path "$env:USERPROFILE\.ssh\id_ed25519_lacabrera"

# Test manual
& "C:\Program Files\Git\usr\bin\ssh.exe" -i "$env:USERPROFILE\.ssh\id_ed25519_lacabrera" -p 5794 root@200.58.127.114 "echo OK"
```

**Frontend no responde en HTTPS:**
```bash
# En el servidor, verificar nginx
systemctl status nginx
nginx -t

# Verificar que frontend esta en puerto 3000
docker ps | grep frontend
```

**API no responde:**
```bash
# Verificar container
docker logs lacabrera-api --tail 50

# Health check directo
curl http://localhost:5000/api/health
```

## Seguridad

- **NUNCA** usar las passwords de ejemplo en produccion
- Cambiar `SA_PASSWORD` y `DB_CONNECTION_STRING` antes de deploy
- Considerar usar Azure Key Vault o similares para secrets
- El archivo `.env` NO debe commitearse al repositorio
- La clave SSH `id_ed25519_lacabrera` debe mantenerse segura
