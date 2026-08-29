# Deploy en VPS

## Requisitos
- Ubuntu 22.04 (o similar)
- Docker instalado
- 4GB RAM minimo
- 20GB disco

## Deploy Rapido

1. Conectar al servidor:
```bash
ssh -p5794 root@200.58.127.114
```

2. Descargar y ejecutar script:
```bash
curl -fsSL https://raw.githubusercontent.com/GitEmilianoRizzo/App_LaCabrera/main/deploy/deploy.sh | bash
```

O manualmente:
```bash
git clone https://github.com/GitEmilianoRizzo/App_LaCabrera.git /opt/lacabrera
cd /opt/lacabrera
chmod +x deploy/deploy.sh
./deploy/deploy.sh
```

## Acceso despues del deploy

- **Dashboard**: http://200.58.127.114
- **API**: http://200.58.127.114:5000
- **Swagger**: http://200.58.127.114:5000/swagger

## Usuario Admin

- Email: admin@lacabrera.com
- Password: Admin123!

## Comandos utiles

```bash
cd /opt/lacabrera

# Ver logs
docker compose -f docker-compose.server.yml logs -f

# Ver estado
docker compose -f docker-compose.server.yml ps

# Reiniciar
docker compose -f docker-compose.server.yml restart

# Parar
docker compose -f docker-compose.server.yml down

# Actualizar
git pull && docker compose -f docker-compose.server.yml up -d --build
```
