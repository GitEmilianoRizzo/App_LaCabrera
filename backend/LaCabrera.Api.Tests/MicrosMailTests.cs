using System.IO.Compression;
using System.Text;
using LaCabrera.Api.Services;
using MimeKit;

namespace LaCabrera.Api.Tests;

public class MicrosMailTests
{
    private static MimeMessage MailConZip(params (string Nombre, string Contenido)[] archivos)
    {
        using var zip = new MemoryStream();
        using (var archivo = new ZipArchive(zip, ZipArchiveMode.Create, true))
            foreach (var (nombre, contenido) in archivos)
            {
                using var w = new StreamWriter(archivo.CreateEntry(nombre).Open(), new UTF8Encoding(false));
                w.Write(contenido);
            }

        var cuerpo = new BodyBuilder { TextBody = "envío diario" };
        cuerpo.Attachments.Add("standard.GeneralLedger_CAB_102726.zip", zip.ToArray(), new ContentType("application", "zip"));
        var mensaje = new MimeMessage { Subject = "GL", Body = cuerpo.ToMessageBody() };
        mensaje.From.Add(MailboxAddress.Parse("envios@grupoloslirios.cl"));
        return mensaje;
    }

    [Fact]
    public void Lee_los_GeneralLedger_dentro_del_zip_y_su_codigo_de_tienda()
    {
        var mail = MailConZip(
            ("GL_CAB_102726.txt", "GLID|CAB|La Cabrera Alonso|20261027|20261027|20261028|075052|1.00|1.00|Simphony|19.9.2\r\nSUM|1\r\n"),
            ("GL_PIE_090726.txt", "GLID|PIE|Otro restaurante|20260907|20260907|20260908|075052|1.00|1.00|Simphony|19.9.2\r\n"),
            ("leeme.pdf", "no es un GL"));

        var archivos = MicrosMailConectorService.ArchivosGeneralLedger(mail).ToList();

        Assert.Equal(2, archivos.Count);
        Assert.Equal(new[] { "CAB", "PIE" }, archivos.Select(a => MicrosGlParser.LeerCodigoTienda(a.Contenido)));
        Assert.EndsWith("/GL_CAB_102726.txt", archivos[0].Nombre);
    }

    [Theory]
    [InlineData("VINOS", "CARMENERE", "WINE")]
    [InlineData("BEBESTIBLES", "CARMENERE  COPA", "WINE")]
    [InlineData("BEBESTIBLES", "COCTELERIA DE LA CASA", "COCKTAIL")]
    [InlineData("BEBESTIBLES", "CAFETERIA", "COFFEE")]
    [InlineData("BEBESTIBLES", "BEBIDAS / AGUAS", "BEVERAGE")]
    [InlineData("ALIMENTOS", "POSTRES ", "DESSERT")]
    [InlineData("ALIMENTOS", "GUARNICIONES", "SIDE_DISH")]
    [InlineData("ALIMENTOS", "ENTRADAS FRIAS.", "STARTER")]
    [InlineData("ALIMENTOS", "CARNES Y POLLOS", "MAIN_COURSE")]
    [InlineData("ALIMENTOS", "MODIFICADORES ALIMENTOS", "OTHER")]
    public void Categoria_estandar_desde_familia_y_subfamilia(string familia, string subfamilia, string esperada) =>
        Assert.Equal(esperada, MicrosGlParser.Categoria(familia, subfamilia));
}
