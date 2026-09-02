# Toast Parser - La Cabrera

Convierte reportes de Toast POS (copiados desde la web) al formato JSON de ingesta de La Cabrera.

## Estructura de carpetas

```
toast_parser/
├── toast_parser.py    # Script principal
├── input/             # Archivos .txt copiados de Toast
├── output/            # JSONs generados listos para ingestar
├── run.bat            # Script para ejecutar el parser
└── README.md
```

## Uso

### 1. Copiar datos de Toast

1. Ir a Toast > Order Details
2. Seleccionar el rango de fechas
3. Seleccionar todo (Ctrl+A) y copiar (Ctrl+C)
4. Pegar en un archivo .txt en la carpeta `input/`
5. Nombrar el archivo descriptivamente, ej: `Miami_Midtown_2026-08-01.txt`

### 2. Ejecutar el parser

**Opción A - Doble click:**
```
run.bat
```

**Opción B - Línea de comandos:**
```bash
# Procesar todos los archivos en input/
python toast_parser.py --input input --output output

# Procesar un archivo específico
python toast_parser.py --input input/Miami_1.txt --output output/Miami_1.json
```

### 3. Subir al sistema

Los archivos JSON generados en `output/` están listos para:
- Subir via API: `POST /api/v1/sales/daily-batch`
- O copiar a la carpeta de Google Drive del conector

## Códigos de franquicia

El parser detecta automáticamente la franquicia por el nombre:

| Nombre en Toast | Código generado |
|-----------------|-----------------|
| La Cabrera Midtown | MIAMI_MIDTOWN |
| La Cabrera Coconut Grove | MIAMI_COCONUT |
| La Cabrera Sunny Isles | MIAMI_SUNNY |
| La Cabrera Brickell | MIAMI_BRICKELL |

## Formato de salida

- `amounts_include_tax: false` - Los montos son ANTES de impuestos
- `tax_amount` y `tax_rate` se calculan automáticamente
- Los tickets sin mesa/mozo se procesan normalmente (delivery/takeout)

## Requisitos

- Python 3.8+
- No requiere dependencias externas (solo librerías estándar)
