# nginx (VPS de producción)

`lacabrera.conf` es el sitio activo de nginx en la VPS (`/etc/nginx/sites-available/lacabrera`, habilitado con un link en `sites-enabled`). Hace HTTPS y reenvía:

- `/` → frontend (`127.0.0.1:8080`)
- `/api/` y `/health` → API (`127.0.0.1:5000`)

Ojo con `client_max_body_size 100M`: sin esa línea nginx rechaza las subidas de más de 1 MB (error 413 al subir archivos de Toast).

## Aplicar un cambio

```bash
cp -p /etc/nginx/sites-available/lacabrera /root/lacabrera.nginx.bak-$(date +%Y%m%d)
cp /opt/lacabrera/deploy/nginx/lacabrera.conf /etc/nginx/sites-available/lacabrera
nginx -t && systemctl reload nginx
```

Si `nginx -t` falla no se recarga nada; volver a la copia `.bak`.
