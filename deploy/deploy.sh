#!/bin/bash
# ============================================
# LA CABRERA - Script de Deploy para VPS
# Ubuntu 22.04 con Docker
# ============================================

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
REPO_URL="https://github.com/GitEmilianoRizzo/App_LaCabrera.git"
APP_DIR="/opt/lacabrera"
SA_PASSWORD="LaCabrera2026!Prod"
JWT_SECRET="LaCabreraJwtSecretKey2026ProductionSecure!"

echo -e "${BLUE}============================================${NC}"
echo -e "${BLUE}  LA CABRERA - Deploy Script${NC}"
echo -e "${BLUE}============================================${NC}"
echo ""

# Function to print status
print_status() {
    echo -e "${GREEN}[OK]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

print_step() {
    echo ""
    echo -e "${BLUE}>>> $1${NC}"
}

# Step 1: System Update
print_step "1. Actualizando sistema..."
apt-get update -y
apt-get upgrade -y
print_status "Sistema actualizado"

# Step 2: Install dependencies
print_step "2. Instalando dependencias..."
apt-get install -y git curl wget
print_status "Dependencias instaladas"

# Step 3: Verify Docker
print_step "3. Verificando Docker..."
if ! command -v docker &> /dev/null; then
    print_error "Docker no esta instalado. Instalando..."
    curl -fsSL https://get.docker.com -o get-docker.sh
    sh get-docker.sh
    systemctl enable docker
    systemctl start docker
fi
docker --version
docker compose version
print_status "Docker verificado"

# Step 4: Clone or update repository
print_step "4. Clonando repositorio..."
if [ -d "$APP_DIR" ]; then
    print_warning "Directorio $APP_DIR ya existe. Actualizando..."
    cd "$APP_DIR"
    git fetch origin
    git reset --hard origin/main
else
    git clone "$REPO_URL" "$APP_DIR"
    cd "$APP_DIR"
fi
print_status "Repositorio clonado en $APP_DIR"

# Step 5: Create .env file
print_step "5. Creando archivo .env..."
cat > "$APP_DIR/.env" << EOF
# ============================================
# LA CABRERA - Configuracion Produccion
# Generado automaticamente: $(date)
# ============================================

# SQL Server
SA_PASSWORD=$SA_PASSWORD
DB_CONNECTION_STRING=Server=sqlserver;Database=LaCabreraDB;User Id=sa;Password=$SA_PASSWORD;TrustServerCertificate=True

# JWT Authentication
JWT_SECRET=$JWT_SECRET
JWT_ISSUER=LaCabrera.Api
JWT_AUDIENCE=LaCabrera.Frontend
JWT_EXPIRATION_MINUTES=60
JWT_REFRESH_EXPIRATION_DAYS=7

# Environment
ASPNETCORE_ENVIRONMENT=Production

# Frontend
VITE_API_URL=/api/v1
EOF
print_status "Archivo .env creado"

# Step 6: Stop existing containers
print_step "6. Deteniendo contenedores existentes..."
docker compose -f docker-compose.server.yml down 2>/dev/null || true
print_status "Contenedores detenidos"

# Step 7: Build and start containers
print_step "7. Construyendo y levantando contenedores..."
docker compose -f docker-compose.server.yml build --no-cache
docker compose -f docker-compose.server.yml up -d
print_status "Contenedores iniciados"

# Step 8: Wait for SQL Server
print_step "8. Esperando que SQL Server este listo..."
echo "Esto puede tomar hasta 60 segundos..."
for i in {1..60}; do
    if docker exec lacabrera-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -C -Q "SELECT 1" &>/dev/null; then
        print_status "SQL Server esta listo"
        break
    fi
    echo -n "."
    sleep 1
done
echo ""

# Step 9: Run database scripts
print_step "9. Ejecutando scripts de base de datos..."

# Create a function to run SQL scripts
run_sql_script() {
    local script=$1
    local script_name=$(basename "$script")
    echo "  Ejecutando: $script_name"
    docker exec -i lacabrera-sqlserver /opt/mssql-tools18/bin/sqlcmd \
        -S localhost -U sa -P "$SA_PASSWORD" -C \
        -i "/scripts/$script_name" 2>&1 || true
}

# Copy scripts to container
docker cp "$APP_DIR/database/scripts" lacabrera-sqlserver:/scripts

# Run scripts in order
SCRIPTS=(
    "00_create_database.sql"
    "01_create_schemas.sql"
    "02_create_tables.sql"
    "03_create_constraints_indexes.sql"
    "04_create_views_dashboard.sql"
    "05_create_stored_procedures_ingestion.sql"
    "06_seed_demo_data.sql"
    "06b_seed_extended_products.sql"
    "07_seed_realistic_sales_data.sql"
)

# Check if auth migration exists and add it
if [ -f "$APP_DIR/database/migrations/020_create_auth_tables.sql" ]; then
    docker cp "$APP_DIR/database/migrations" lacabrera-sqlserver:/migrations
    SCRIPTS+=("../migrations/020_create_auth_tables.sql")
fi

for script in "${SCRIPTS[@]}"; do
    if [[ $script == *"migrations"* ]]; then
        script_path="/migrations/$(basename $script)"
    else
        script_path="/scripts/$script"
    fi
    echo "  Ejecutando: $(basename $script)"
    docker exec lacabrera-sqlserver /opt/mssql-tools18/bin/sqlcmd \
        -S localhost -U sa -P "$SA_PASSWORD" -C \
        -i "$script_path" 2>&1 | grep -v "^$" | head -5 || true
done

print_status "Scripts de base de datos ejecutados"

# Step 10: Verify deployment
print_step "10. Verificando deploy..."
echo ""
echo "Contenedores:"
docker compose -f docker-compose.server.yml ps
echo ""

# Health checks
echo "Health checks:"
sleep 5  # Give API time to fully start

if curl -s -f http://localhost:5000/api/health > /dev/null; then
    print_status "API: OK"
else
    print_warning "API: No responde aun (puede tomar unos segundos mas)"
fi

if curl -s -f http://localhost:80/health > /dev/null; then
    print_status "Frontend: OK"
else
    print_warning "Frontend: No responde aun"
fi

# Final message
echo ""
echo -e "${GREEN}============================================${NC}"
echo -e "${GREEN}  Deploy completado!${NC}"
echo -e "${GREEN}============================================${NC}"
echo ""
echo "Accesos:"
echo "  - Dashboard: http://$(hostname -I | awk '{print $1}')"
echo "  - API:       http://$(hostname -I | awk '{print $1}'):5000"
echo "  - API Docs:  http://$(hostname -I | awk '{print $1}'):5000/swagger"
echo ""
echo "Usuario admin por defecto:"
echo "  - Email: admin@lacabrera.com"
echo "  - Password: Admin123!"
echo ""
echo "SQL Server:"
echo "  - Host: localhost:1433"
echo "  - Usuario: sa"
echo "  - Password: $SA_PASSWORD"
echo ""
echo "Comandos utiles:"
echo "  - Ver logs: docker compose -f docker-compose.server.yml logs -f"
echo "  - Reiniciar: docker compose -f docker-compose.server.yml restart"
echo "  - Parar: docker compose -f docker-compose.server.yml down"
echo ""
