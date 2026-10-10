# Backup diario de LaCabreraDB

`backup-db.sh` hace un backup comprimido y verificado de `LaCabreraDB`, lo guarda en
`/var/backups/lacabrera` (últimos 7 días) y lo sube a OneDrive (últimos 30 días) con rclone.

## Instalación en la VPS (una sola vez)

```bash
# 1. rclone
curl -fsSL https://rclone.org/install.sh | bash

# 2. Remoto de OneDrive llamado "onedrive"
#    La VPS no tiene navegador: el permiso se da desde una PC con navegador.
#    En la PC (Windows): bajar rclone de https://rclone.org/downloads/ y correr
#        .\rclone.exe authorize "onedrive"
#    Iniciar sesión con la cuenta de Microsoft donde van a quedar los backups y copiar el token que imprime.
#    En la VPS:
rclone config      # n (nuevo) > nombre: onedrive > tipo: onedrive > client_id/secret: vacío
                   # > region: global > advanced: n > auto config: n > pegar el token
                   # > tipo de cuenta: OneDrive Personal or Business > elegir el drive > y > q
rclone mkdir onedrive:LaCabrera/Backups

# 3. Programar
cp /opt/lacabrera/deploy/backup/lacabrera-backup.cron /etc/cron.d/lacabrera-backup
chmod 644 /etc/cron.d/lacabrera-backup

# 4. Probar a mano
/opt/lacabrera/deploy/backup/backup-db.sh
```

## Controles

- Log: `tail -50 /var/log/lacabrera-backup.log`
- Backups locales: `ls -lh /var/backups/lacabrera`
- Backups en OneDrive: `rclone ls onedrive:LaCabrera/Backups`
- El script termina con código 2 si no pudo subir a OneDrive (el backup local igual queda hecho).

## Restaurar

```bash
docker cp /var/backups/lacabrera/LaCabreraDB_AAAAMMDD_HHMM.bak lacabrera-sqlserver:/var/opt/mssql/backup/
# Restaurar SIEMPRE con otro nombre primero (LaCabreraDB_restore) y verificar antes de reemplazar la base real.
```
