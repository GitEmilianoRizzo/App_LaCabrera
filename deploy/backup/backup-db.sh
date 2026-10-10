#!/bin/bash
# =====================================================
# Backup diario de LaCabreraDB (producción)
#
# 1. BACKUP DATABASE comprimido y con CHECKSUM dentro del contenedor de SQL Server
# 2. RESTORE VERIFYONLY para comprobar que el archivo sirve
# 3. Copia a /var/backups/lacabrera en la VPS (se conservan DIAS_LOCAL días)
# 4. Si hay un remoto de rclone configurado (Google Drive / OneDrive), lo sube
#    y borra allá los backups con más de DIAS_REMOTO días
#
# La contraseña de sa NO está en este script: se usa la variable MSSQL_SA_PASSWORD
# que ya tiene el contenedor.
#
# Uso: /opt/lacabrera/deploy/backup/backup-db.sh
# Programado por /etc/cron.d/lacabrera-backup (ver README.md de esta carpeta)
# =====================================================
set -euo pipefail

CONTENEDOR="${CONTENEDOR:-lacabrera-sqlserver}"
BASE="${BASE:-LaCabreraDB}"
DIR_LOCAL="${DIR_LOCAL:-/var/backups/lacabrera}"
DIAS_LOCAL="${DIAS_LOCAL:-7}"
RCLONE_REMOTO="${RCLONE_REMOTO:-gdrive:LaCabrera/Backups}"
DIAS_REMOTO="${DIAS_REMOTO:-30}"
DIR_CONTENEDOR="/var/opt/mssql/backup"

FECHA="$(date +%Y%m%d_%H%M)"
ARCHIVO="${BASE}_${FECHA}.bak"

log() { echo "[$(date '+%Y-%m-%d %H:%M:%S')] $*"; }

# Evitar dos backups en paralelo
exec 9>/var/lock/lacabrera-backup.lock
flock -n 9 || { log "Ya hay un backup en curso, se omite"; exit 0; }

mkdir -p "$DIR_LOCAL"

sql() {
    docker exec "$CONTENEDOR" bash -c \
        '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b -Q "$1"' _ "$1"
}

log "Backup de $BASE -> $ARCHIVO"
docker exec "$CONTENEDOR" mkdir -p "$DIR_CONTENEDOR"
sql "BACKUP DATABASE [$BASE] TO DISK = N'$DIR_CONTENEDOR/$ARCHIVO' WITH COPY_ONLY, COMPRESSION, CHECKSUM, INIT, STATS = 25"
sql "RESTORE VERIFYONLY FROM DISK = N'$DIR_CONTENEDOR/$ARCHIVO' WITH CHECKSUM"

docker cp "$CONTENEDOR:$DIR_CONTENEDOR/$ARCHIVO" "$DIR_LOCAL/$ARCHIVO"
docker exec "$CONTENEDOR" rm -f "$DIR_CONTENEDOR/$ARCHIVO"
log "Guardado en $DIR_LOCAL/$ARCHIVO ($(du -h "$DIR_LOCAL/$ARCHIVO" | cut -f1))"

find "$DIR_LOCAL" -name "${BASE}_*.bak" -mtime +"$DIAS_LOCAL" -print -delete | sed 's/^/Borrado local: /'

if command -v rclone >/dev/null && rclone listremotes | grep -qx "${RCLONE_REMOTO%%:*}:"; then
    rclone copy "$DIR_LOCAL/$ARCHIVO" "$RCLONE_REMOTO" --retries 5
    log "Subido a $RCLONE_REMOTO"
    rclone delete "$RCLONE_REMOTO" --min-age "${DIAS_REMOTO}d" --include "${BASE}_*.bak"
else
    log "AVISO: rclone sin remoto '${RCLONE_REMOTO%%:*}' configurado; el backup quedó solo en la VPS"
    exit 2
fi

log "OK"
