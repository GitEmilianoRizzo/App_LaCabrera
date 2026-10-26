namespace LaCabrera.Api.Services;

/// <summary>
/// Acumula el resultado de una extracción día por día.
/// Un día que no se pudo extraer (Success = false) cuenta como error aunque no traiga ErrorsCount,
/// para que un rango con fallas no se informe como éxito con 0 tickets.
/// </summary>
public class ResumenRangoExtraccion
{
    private int _diasOk;
    private int _tickets;
    private int _lineas;
    private int _errores;
    private readonly List<string> _diasConError = new();
    private string? _primerError;

    public int DiasConError => _diasConError.Count;

    public void Agregar(DateTime fecha, EjecucionResultDto dia)
    {
        _tickets += dia.TicketsProcesados;
        _lineas += dia.LineasProcesadas;

        if (dia.Success)
        {
            _diasOk++;
            _errores += dia.ErrorsCount;
        }
        else
        {
            AgregarError(fecha, dia.Message);
            _errores += Math.Max(1, dia.ErrorsCount);
        }
    }

    public void AgregarExcepcion(DateTime fecha, Exception ex)
    {
        AgregarError(fecha, ex.Message);
        _errores++;
    }

    private void AgregarError(DateTime fecha, string? mensaje)
    {
        _diasConError.Add(fecha.ToString("yyyy-MM-dd"));
        _primerError ??= $"{fecha:yyyy-MM-dd}: {mensaje}";
    }

    /// <param name="diasSinProcesar">Días que quedaron sin intentar porque se cortó el rango.</param>
    public EjecucionResultDto Resultado(int diasSinProcesar = 0)
    {
        var mensaje = $"Rango completado: {_diasOk} días procesados, {_tickets} tickets, {_lineas} líneas";
        if (_diasConError.Count > 0)
            mensaje += $"; {_diasConError.Count} días con error ({string.Join(", ", _diasConError.Take(5))}{(_diasConError.Count > 5 ? ", ..." : "")}). Primer error: {_primerError}";
        if (diasSinProcesar > 0)
            mensaje += $"; se cortó el rango y quedaron {diasSinProcesar} días sin procesar";

        return new EjecucionResultDto
        {
            Success = _diasConError.Count == 0 && diasSinProcesar == 0,
            Message = mensaje,
            TicketsProcesados = _tickets,
            LineasProcesadas = _lineas,
            ErrorsCount = _errores
        };
    }
}
