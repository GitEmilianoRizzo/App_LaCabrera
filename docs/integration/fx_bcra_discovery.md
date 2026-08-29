# Discovery: API BCRA Estadísticas Cambiarias

**Fecha:** 2026-08-22
**Lote:** MEJORAS_20260822 - Etapa 2.0
**Autor:** Claude Code
**Estado:** ✅ CONFIRMADO (2026-08-22)

## Decisiones Confirmadas:
- PHP requerido (franquicia Manila) → Usar proveedor secundario
- Interpretación tipoPase confirmada
- Job a las 12:00 ARG

---

## 1. Endpoints Consultados

| Endpoint | Descripción |
|----------|-------------|
| `GET /Maestros/Divisas` | Lista de monedas disponibles |
| `GET /Cotizaciones?fecha=YYYY-MM-DD` | Cotizaciones de todas las monedas para una fecha |
| `GET /Cotizaciones/{codMoneda}` | Histórico de cotizaciones de una moneda |

**Base URL:** `https://api.bcra.gob.ar/estadisticascambiarias/v1.0`

---

## 2. Monedas Disponibles

### Resultado de consulta real a `/Maestros/Divisas`:

| Código | Denominación | Requerida | Disponible |
|--------|--------------|-----------|------------|
| USD | DOLAR E.E.U.U. | ✅ | ✅ SÍ |
| ARS | PESO | ✅ | ✅ SÍ |
| EUR | EURO | ✅ | ✅ SÍ |
| COP | COLOMBIANOS | ✅ | ✅ SÍ |
| PYG | GUARANIES | ✅ | ✅ SÍ |
| PHP | Peso Filipino | ✅ | ❌ **NO DISPONIBLE** |

### ⚠️ ALERTA: PHP no está en BCRA

El peso filipino (PHP) NO está disponible en la API del BCRA.
**Acción requerida:** Definir proveedor secundario para PHP o confirmar si hay franquicias en Filipinas.

---

## 3. Semántica de tipoPase y tipoCotización

### Respuesta de ejemplo (2026-08-21):

```json
{
  "codigoMoneda": "EUR",
  "descripcion": "EURO",
  "tipoPase": 1.16810000,
  "tipoCotizacion": 1750.98000000
}
```

```json
{
  "codigoMoneda": "USD",
  "descripcion": "DOLAR E.E.U.U.",
  "tipoPase": 0.00000000,
  "tipoCotizacion": 1499.00000000
}
```

### Interpretación Confirmada:

| Campo | Significado | Ejemplo EUR |
|-------|-------------|-------------|
| `tipoPase` | USD equivalentes a 1 unidad de la moneda | 1 EUR = 1.168 USD |
| `tipoCotizacion` | ARS necesarios para comprar 1 unidad de la moneda | 1 EUR = 1750.98 ARS |

### Para USD específicamente:
- `tipoPase = 0` (no aplica, es la moneda de referencia)
- `tipoCotizacion = 1499` → **1 USD = 1499 ARS** (tasa oficial)

---

## 4. Cálculo de UnidadesPorUsd

### Fórmula del proyecto:
```
UnidadesPorUsd = cuántas unidades de esa moneda equivalen a 1 USD
ImporteUsd = ImporteLocal / UnidadesPorUsd
```

### Conversión desde datos BCRA:

| Moneda | Fórmula | Ejemplo |
|--------|---------|---------|
| **ARS** | `UnidadesPorUsd = tipoCotizacion(USD)` | 1499 ARS = 1 USD |
| **EUR** | `UnidadesPorUsd = 1 / tipoPase` | 1/1.168 = 0.856 EUR = 1 USD |
| **USD** | `UnidadesPorUsd = 1` (constante) | 1 USD = 1 USD |
| **Otras** | `UnidadesPorUsd = 1 / tipoPase` | Calcular según tipoPase |

### Validación cruzada EUR:
- Si 1 EUR = 1.168 USD
- Entonces 1 USD = 0.856 EUR
- 100 EUR / 0.856 = 116.8 USD ✅

### Validación cruzada ARS:
- Si 1 USD = 1499 ARS
- Entonces UnidadesPorUsd = 1499
- 100,000 ARS / 1499 = 66.71 USD ✅

---

## 5. Comportamiento de Fines de Semana y Feriados

### Consulta a sábado 2026-08-15:

```json
{
  "status": 200,
  "metadata": { "resultset": { "count": 0 } },
  "results": {
    "fecha": null,
    "detalle": []
  }
}
```

### ⚠️ HALLAZGO CRÍTICO:

La API **NO devuelve automáticamente** la cotización del viernes anterior.
Devuelve array vacío con HTTP 200.

**Acción requerida en el código:**
1. Si la fecha consultada devuelve vacío → buscar fecha anterior
2. Al encontrar cotización, marcar `EsArrastrada = 1`
3. Guardar `FechaCotizacionOrigen` apuntando al día real de publicación

---

## 6. Precisión Decimal

| Campo | Precisión Observada |
|-------|---------------------|
| `tipoPase` | 8 decimales |
| `tipoCotizacion` | 8 decimales |

**Recomendación:** Usar `DECIMAL(18,8)` en SQL Server (ya definido en script 09).

---

## 7. Rate Limits y Headers

| Aspecto | Observación |
|---------|-------------|
| Rate limits | No documentados oficialmente. Observado OK con múltiples requests. |
| User-Agent | No requerido explícitamente |
| API Key | **NO REQUERIDA** - API pública |
| Autenticación | Ninguna |

**Recomendación:** Implementar delay de 100-200ms entre requests por precaución.

---

## 8. Rango de Fechas y Paginación

- **Rango histórico:** La API parece tener datos desde hace varios años
- **Paginación:** No observada para el endpoint por fecha
- **Endpoint por moneda:** Devuelve histórico completo (posible paginación implícita)

**Para carga inicial:** Consultar fecha por fecha desde 2026-06-01.

---

## 9. Monedas de Franquicias Activas

| Franquicia | País | Moneda | En BCRA |
|------------|------|--------|---------|
| Paraguay Asunción | Paraguay | PYG | ✅ |
| Aeroparque | Argentina | ARS | ✅ |
| Ezeiza | Argentina | ARS | ✅ |
| Marbella | España | EUR | ✅ |
| Miami Beach | USA | USD | ✅ (base) |
| (Futuras Filipinas) | Filipinas | PHP | ❌ |

---

## 10. Decisiones Pendientes de Confirmación

### ⚠️ ANTES DE IMPLEMENTAR, CONFIRMAR:

1. **PHP (Peso Filipino):** ¿Hay franquicias activas o planificadas en Filipinas?
   - Si SÍ → Definir proveedor secundario (OpenExchangeRates, exchangerate-api.com)
   - Si NO → Ignorar PHP por ahora

2. **Dirección de tipoPase confirmada:**
   - `tipoPase` = USD por 1 unidad de moneda extranjera
   - Para convertir a UnidadesPorUsd: **INVERTIR** el valor (1/tipoPase)
   - Para ARS: usar `tipoCotizacion` del USD directamente

3. **Horario del job:**
   - BCRA publica ~11:00 hora Argentina (UTC-3)
   - Recomendación: Job a las 12:00 ARG para asegurar disponibilidad

---

## 11. Pseudocódigo de Conversión

```csharp
public decimal CalcularUnidadesPorUsd(string codigoMoneda, BcraResponse response)
{
    if (codigoMoneda == "USD")
        return 1.0m;

    if (codigoMoneda == "ARS")
    {
        // Para ARS, usar tipoCotizacion del USD
        var usdCotizacion = response.Detalle
            .First(d => d.CodigoMoneda == "USD")
            .TipoCotizacion;
        return usdCotizacion; // Ej: 1499 ARS = 1 USD
    }

    // Para otras monedas, invertir tipoPase
    var monedaCotizacion = response.Detalle
        .First(d => d.CodigoMoneda == codigoMoneda);

    if (monedaCotizacion.TipoPase == 0)
        throw new Exception($"tipoPase=0 para {codigoMoneda}");

    return 1.0m / monedaCotizacion.TipoPase; // Ej: 1/1.168 = 0.856 EUR por USD
}
```

---

## 12. Ejemplo de Payload Real

### Request:
```
GET https://api.bcra.gob.ar/estadisticascambiarias/v1.0/Cotizaciones?fecha=2026-08-21
```

### Response (extracto):
```json
{
  "status": 200,
  "metadata": { "resultset": { "count": 44 } },
  "results": {
    "fecha": "2026-08-21T00:00:00",
    "detalle": [
      { "codigoMoneda": "USD", "descripcion": "DOLAR E.E.U.U.", "tipoPase": 0.0, "tipoCotizacion": 1499.0 },
      { "codigoMoneda": "EUR", "descripcion": "EURO", "tipoPase": 1.1681, "tipoCotizacion": 1750.98 },
      { "codigoMoneda": "PYG", "descripcion": "GUARANIES", "tipoPase": 0.00013, "tipoCotizacion": 0.19 },
      { "codigoMoneda": "COP", "descripcion": "COLOMBIANOS", "tipoPase": 0.00024, "tipoCotizacion": 0.36 }
    ]
  }
}
```

---

## RESUMEN EJECUTIVO

| Aspecto | Estado |
|---------|--------|
| Monedas principales (USD, ARS, EUR, PYG, COP) | ✅ Disponibles |
| PHP (Peso Filipino) | ❌ No disponible - requiere proveedor secundario |
| Dirección de tipoPase | ✅ Confirmada (USD por unidad de moneda) |
| Fines de semana | ⚠️ Devuelve vacío, implementar arrastre |
| Rate limits | ℹ️ No documentados, precaución recomendada |
| API Key | ✅ No requerida |

---

**PARADA OBLIGATORIA:** Esperar confirmación del usuario antes de proceder con Etapa 2.1.
