using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace LaCabrera.Api.Services.Consumo;

/// <summary>Resultado de clasificar un nombre de producto del POS.</summary>
public sealed record ProductoClasificado(
    string Rubro, string Producto, bool EsCombo, bool EsMediaPorcion, string Premium, int? Gramos, int? UnidadesPorItem)
{
    public bool EsProducto => Rubro != ClasificadorProductos.RubroNoProducto && Rubro != ClasificadorProductos.RubroSinClasificar;
}

/// <summary>
/// Catálogo canónico de La Cabrera: lleva cada nombre crudo del POS ("BIFE ANCHO 400GR", "RIBEYE 14 OZ", "OJO DE BIFE WAGYU")
/// a un rubro y un producto canónico ("CARNES VACUNAS" / "Ojo de bife / rib eye").
/// Las reglas van en orden y gana la primera que matchea: el orden importa ("provoleta con tocino" es provoleta, no cerdo).
/// Origen: análisis de consumo por producto y zona (nov-2026), 99,9 % de la venta clasificada.
/// </summary>
public static class ClasificadorProductos
{
    public const string RubroNoProducto = "NO PRODUCTO";
    public const string RubroSinClasificar = "SIN CLASIFICAR";
    public const string ProductoSinClasificar = "Sin clasificar";

    private const RegexOptions Op = RegexOptions.CultureInvariant | RegexOptions.Compiled;

    private sealed record Regla(string Rubro, string Producto, Regex Patron);

    private static Regla R(string rubro, string producto, string patron) => new(rubro, producto, new Regex(patron, Op));

    private static readonly Regla[] Reglas =
    {
        R(@"NO PRODUCTO", @"Propina / servicio", @"GORJETA|PROPINA|\bTIPS?\b|SERVICE CHARGE|TAXA DE SERV|\bSERVICIO\b|COUVERT ART"),
        R(@"NO PRODUCTO", @"Eventos / paquetes", @"MENU BIG BOX|INAGURACION|INAUGURACION|\bEVENT|PACKAGE|DEPOSIT|GIFT ?CARD|CERTIFICADO|VOUCHER|BUYOUT|PRIVATE DINING"),
        R(@"NO PRODUCTO", @"Ajustes / varios", @"CAMBIO PRODUCTO|CUCHILLO|SPECIAL .*CORP|^MENU$|DESCUENTO|DISCOUNT|FACTURA A/B|OPEN (FOOD|ITEM|BAR)|^VARIOS|^AJUSTE|DELIVERY|\bENVIO\b|\bBOLSA\b|CORKAGE|DESCORCHE|^ITEM ABIERTO"),
        R(@"NO PRODUCTO", @"Modificadores e instrucciones de cocina", @"^A PUNTO$|ANTES DE PUNTO|PASADO DE PUNTO|^MENSAJE|^JUGOS[OA]$|^COCIDO$|^FIRE$|2DO TIEMPO|^PAGGER|^=+$|CENTAVO|^EXPERIENCIA LA CABRERA|^CALIENTES$|^TERMINO|BIEN COCID|^RARE$|^MEDIUM( RARE| WELL)?$|^WELL DONE$|^SIN SAL$|^PARA LLEVAR$|^TAKE ?OUT$"),
        R(@"CORTESÍAS (SIN CARGO)", @"Cortesías de la casa", @"CALENTITOS|WARMIES|CORTESIA|^PANERA|^LUPA$|^LUPITAS$|CALDO DE RES|BIRTHDAY|CUMPLEANOS|^GAZPACHO$"),
        R(@"COCTELERÍA Y DESTILADOS", @"Sangría", @"SANGRIA|SANGRYA"),
        R(@"COCTELERÍA Y DESTILADOS", @"Aperol / spritz", @"APEROL|SPRITZ"),
        R(@"COCTELERÍA Y DESTILADOS", @"Gin tonic / gin", @"\bGIN\b|GIN ?TONIC|HENDRICK|TANQUERAY|BOMBAY|BEEFEATER|MONKEY 47|GORDON"),
        R(@"COCTELERÍA Y DESTILADOS", @"Fernet", @"FERNET|BRANCA"),
        R(@"COCTELERÍA Y DESTILADOS", @"Cócteles clásicos", @"NEGRONI|MOJITO|MARGARITA|OLD FASHION|MARTINI|\bSOUR\b|CAIPIRI|CAIPIROS|MIMOSA|COSMOPOLITAN|CARAJILLO|MANHATTAN|DAIQUIRI|PALOMA|MULE|BELLINI|MICHELAD|LONG ISLAND|JULEP|ST\.? ?GERM|BLOODY|CUBA LIBRE|PISCOLA|COCKTAIL|COCTEL|TRAGO|SOPHIA LOREN|LA CABRERA SOUR|CLERICO|TINTO DE VERANO|BOULEVARDIER|PINA COLADA|ROSSINI|AMERICANO ROSSO|GARIBALDI|SPRITE CAMPARI|CAMPARI|PENICILIN|SWEET FIRE|WISH ME WELL|GREEN SPRING|LADY IN RED|PERRO CALLEJERO|SPELLBOUND"),
        R(@"COCTELERÍA Y DESTILADOS", @"Destilados", @"^TR |HIBIKI|BAJATIVO|WOODFORD|ANGELS ENVY|BASIL HAYDEN|OLD PARR|BLACK LABEL|DOBEL|VODKA|TITO'?S|GREY GOOSE|ABSOLUT|BELVEDERE|KETEL|WHISK|JOHNNIE|J\.? ?WALKER|JACK DANIEL|CHIVAS|BUCHANAN|MACALLAN|GLENFIDD|GLENLIV|BOURBON|MAKER'?S|JAMESON|TEQUILA|DON JULIO|PATRON|CASAMIGOS|1800|MEZCAL|\bRON\b|\bRUM\b|BACARDI|HAVANA|ZACAPA|DIPLOMATICO|PISCO|COGNAC|HENNESSY|BRANDY|LICOR|BAILEYS|LIMONCELLO|AMARETTO|JAGER|SAMBUCA|GRAPPA|ORUJO|PACHARAN|\bSHOT\b|CACHACA|AGUARDIENTE|ANTIOQUENO|CINZANO|MARTINI ROSSO|VERMUT|VERMOUTH|PORTO\b|OPORTO|COINTREAU|GRAND MARNIER|DRAMBUIE|KAHLUA|FRANGELICO|MALIBU|CHINCHON|CHUPITO|DISARONNO|CLASE AZUL|CASA DRAGONES|DON FULANO|BULLEIT|LEMONCELLO|BAILEY"),
        R(@"CERVEZA", @"Cerveza", @"CERVEZA|\bCERV\b|CHOPP|\bBEER\b|STELLA|QUILMES|CORONA|PATAGONIA|CLUB COLOMBIA|HEINEKEN|BRAHMA|BUDWEISER|MICHELOB|AGUILA|\bPOKER\b|IMPERIAL|KUNSTMANN|\bCRISTAL\b|ESCUDO|ESTRELLA|MAHOU|VICTORIA|INNIS|ERDINGER|AUSTRAL|\bPINTA\b|\bCANA\b|LA COLORADA|STOUT|ALHAMBRA|\bIPA\b|LAGER|DRAFT|PERONI|MODELO|PILSEN|AMSTEL|SAN MIGUEL|ORIGINAL 600|SPATEN|EISENBAHN|BADEN|BLUE MOON|GUINNESS|PACIFICO|DOS EQUIS|\bXX\b|SAPPORO|ASAHI|LONG ?NECK|BOHEMIA|STELLA ARTOIS|COSTUMBRISTA|BBC |TRES CORDILLERAS|ROYAL GUARD|BAVARIA|SOL\b"),
        R(@"BEBIDAS SIN ALCOHOL", @"Limonada / jugos", @"LIMONADA|LEMONADE"),
        R(@"VINO", @"Espumante / champagne", @"\bESP\b|PICC?OLO|CHAMPAGNE|CHAMPAN|ESPUMANTE|ESPUMOSO|PROSECCO|\bCAVA\b|CHANDON|MOET|VEUVE|BRUT|SPARKLING|DOM PERIGNON|CRISTAL ROEDERER|FREIXENET|CODORNIU|EXTRA BRUT|ESPUMA"),
        R(@"VINO", @"Vino por copa", @"COPA (DE )?VINO|VINO (DE LA CASA|COPA|TINTO|BLANCO)|VINHO .*TACA|\bTACA\b|\bGLASS\b|\bCP\.? |^CP\b|\bCOPA\b(?!.*HELADO)"),
        R(@"VINO", @"Vino por botella", @"\bVINO\b|\bVINHO\b|^BT |\bBT\b|BOTELLA|\bBOT\b|MALBEC|\bMB\b|CABERNET|\bCS\b|CAB FRANC|\bCAB\b|\bCF\b|CARMENERE|CRIANZA|\bRSV\b|\bGRSV\b|BRUNELLO|BORDEAUX|CHATEAU|BOLGHERI|MOSCATO|\bVTT\b|SINGLE VINEYARD|BAROLO|CHIANTI|HUENTALA|DUTTON|CODAX|RISCAL|\bPN\b|\bBL\b|LINDA ?FLOR|PINOT|SYRAH|SHIRAZ|MERLOT|CHARDONNAY|SAUVIGNON|BLEND|TORRONTES|BONARDA|TEMPRANILLO|RIOJA|DUERO|ALBARINO|VERDEJO|RESERVA|GRAN RESERVA|CATENA|RUTINI|PERDICES|ENEMIGO|ZAPATA|OPUS ONE|TOMERO|SALENTEIN|PORTILLO|MUGA|NAVARRO|LA CELIA|SEPTIMA|BOUGAINVILLE|MALMA|TRAPICHE|ZUCCARDI|NORTON|ALAMOS|TERRAZAS|LUIGI BOSCA|ACHAVAL|CHEVAL|PULENTA|NIETO|SUSANA BALBO|EL ESTECO|COLOME|DV CATENA|ANGELICA|ALTA VISTA|KAIKEN|MONTES|CASILLERO|CONCHA Y TORO|SANTA RITA|ERRAZURIZ|LAPOSTOLLE|MIGUEL TORRES|PROTOS|VEGA SICILIA|PESQUERA|MARQUES|RAMON BILBAO|ROSE\b|ROSADO|TINTO\b|TINTOS|TINTAS|CEPAS|\bSAV\b|BLANC\b|\bVBR\b|\bVRS\b|MOSCATEL|CUPRA|BLANCO\b|DOC\b|BENMARCO|TRIVENTO|PASCUAL TOSO|FINCA|BODEGA|ESTANCIA|MAYACABA|GRAN ENEMIGO|NICOLAS|ADRIANNA|ICONO"),
        R(@"CAFÉ E INFUSIONES", @"Desayuno / merienda (combo)", @"^(INF|INFUSION|CAFE|DESAYUNO|MERIENDA)\b.*\+|DESAYUNO|MERIENDA"),
        R(@"CAFÉ E INFUSIONES", @"Café", @"(?<!MARTINI )ESPRESSO(?! MARTINI)|EXPRESSO|CORTADO|\bCAFE\b|CAFEZINHO|CAPUCCINO|CAPPUCCINO|CAPUCHINO|LATTE|(?<!ASADO )\bAMERICANO\b|MACCHIATO|MACHIATO|FLAT WHITE|LAGRIMA|RISTRETTO|\bCOFFEE\b|DESCAFEINADO|CAFE CON LECHE|MOCACCINO|TINTO CAFE|CORTADITO|\bCARIOCA\b"),
        R(@"CAFÉ E INFUSIONES", @"Té e infusiones", @"\bTE\b|\bTEA\b|INFUSION|\bCHAS?\b|TISANA|MANZANILLA|ROOIBOS|CHOCOLATE CALIENTE|SUBMARINO|MATE COCIDO|AROMATICA"),
        R(@"BEBIDAS SIN ALCOHOL", @"Gaseosas y sodas", @"AGUA TONICA|TONIC WATER|SODA WATER"),
        R(@"BEBIDAS SIN ALCOHOL", @"Agua", @"\bAGUA\b|WATER|ACQUA|\bPANNA\b(?! ?COTTA)|PELLEGRINO|PERRIER|EVIAN|VOSS|TOPO CHICO|CACHANTUN|BENEDICTINO|VICHY|SOLAN|FONT VELLA|AGUA MINERAL|MANANTIAL|CRISTALINA|NESTLE PURE|BONAQUA|CRYSTAL GEYSER|FIJI|S\.? ?PELLEGRINO"),
        R(@"BEBIDAS SIN ALCOHOL", @"Limonada / jugos", @"LIMONADA|LEMONADE|JUGO|ZUMO|PINA HIERBABUENA|\bJUICE\b|\bSUCO\b|NECTAR|NARANJA EXPRIMIDA|ORANGE|SMOOTHIE|LICUADO|BATIDO|MILKSHAKE|MOCKTAIL|PINK LEMONADE|ICED TEA|TE HELADO|ARNOLD PALMER|SHIRLEY TEMPLE|NARANJADA|FRAPPE|GRANIZADO|LULO|MARACUYA|MORA\b|COROZO"),
        R(@"BEBIDAS SIN ALCOHOL", @"Gaseosas y sodas", @"COCA|\bCOKE\b|PEPSI|SPRITE|FANTA|7 ?UP|SEVEN UP|GASEOSA|GINGER|TONICA|TONIC|SCHWEPPES|GUARANA|\bSODA\b|REFRESCO|DR\.? PEPPER|MOUNTAIN DEW|PASO DE LOS TOROS|MIRINDA|POSTOBON|COLOMBIANA|QUATRO|H2OH|AQUARIUS|KAS\b|NESTEA|RED ?BULL|MONSTER|ENERGIZANTE|LATA|GARRAFA|SIN AZUCAR|ZERO|LIGHT\b|DIET"),
        R(@"SÁNDWICHES Y HAMBURGUESAS", @"Sándwich de ojo de bife", @"\bSAND(WICH)?\b.*OJO|OJO .*\bSAND(WICH)?\b|SANDUCHE.*OJO"),
        R(@"SÁNDWICHES Y HAMBURGUESAS", @"Lomito", @"LOMITO|BARROS LUCO|CHURRASCO"),
        R(@"SÁNDWICHES Y HAMBURGUESAS", @"Choripán", @"CHORIPAN|\bCHORI\b"),
        R(@"SÁNDWICHES Y HAMBURGUESAS", @"Hamburguesa", @"HAMB|BURGER|SLIDER"),
        R(@"SÁNDWICHES Y HAMBURGUESAS", @"Otros sándwiches", @"\bSAND\b|SANDWICH|SANDUCHE|SANGUCHE|CIABATTA|BAGUETTE|WRAP|\bPANINI\b|TOSTADO|MIGA|BAURU|PEBETE|CHIVITO|MEDIALUNA J|BAGEL|TOAST\b"),
        R(@"ENTRADAS", @"Provoleta", @"PROVOLETA|PROVOLONE|PROVO\b|\bPROV\b"),
        R(@"ENTRADAS", @"Empanadas", @"EMPANAD|EMPANA\b"),
        R(@"ENSALADAS", @"Ensaladas", @"ENSALADA|ENSALDA|\bENS\b|SALAD|CAESAR|CESAR|RUCULA|ARUGULA|CAPRES|\bPPT\b|PALTA.*PALMITO|PALMITO|MIXED GREENS|VERDES|LECHUGA|TOMATE.*CEBOLLA"),
        R(@"PASTAS", @"Pastas", @"SPAGUET|BOLOGNESA|RAVIOL|NOQUI|GNOCCHI|SPAGHETTI|ESPAGUETI|PENNE|PASTA|FETTUC|LASAGN|TALLARIN|SORRENTINO|CANELON|FIDEOS|LINGUINI|TAGLIATELLE|AGNOLOT|FUS+ILLI|CAPELETTI|MACARRON|RIGATONI|SCARPARIELLO|PAPPARDELLE"),
        R(@"ACHURAS Y EMBUTIDOS", @"Mollejas", @"MOLLEJ|SWEETBREAD"),
        R(@"ACHURAS Y EMBUTIDOS", @"Chinchulines", @"CHINC?HUL|TRIPA"),
        R(@"ACHURAS Y EMBUTIDOS", @"Morcilla", @"MORCILLA|BLOOD SAUSAGE|MATRIMONIO"),
        R(@"ACHURAS Y EMBUTIDOS", @"Riñones y otras achuras", @"RINON|ACHURA|PICADA DE ACHURAS|CORAZON|RINONCITO"),
        R(@"CARNES VACUNAS", @"Bife de chorizo / NY strip", @"BIFE (DE )?CHORIZO|BIFE CHORIZO|NEW YORK|\bNY\b|STRIP|SIRLOIN|MARIPOSA|LOMO ALTO|CONTRAFILE|BIFE ANGOSTO|ENTRECOT"),
        R(@"ACHURAS Y EMBUTIDOS", @"Chorizo", @"CHORIZO|SAUSAGE|LINGUICA|SALCHICH"),
        R(@"MILANESAS", @"Milanesa", @"MILANESA|\bMILA\b|SCHNITZEL|BREADED|EMPANIZAD|APANAD"),
        R(@"CERDO", @"Cerdo", @"CERDO|PORK|BONDIOLA|SECRETO IBERICO|MATAMBRITO"),
        R(@"CARNES VACUNAS", @"Cortes con hueso (T-bone, tomahawk, chuletón)", @"T-?BONE|T RACK|TOMAHAWK|CHULETON|COWBOY|PORTERHOUSE|BONE[- ]IN|CHULETA DE VACA|BIFE CON HUESO|PRIME RIB|DRY AGED?\b"),
        R(@"CARNES VACUNAS", @"Ojo de bife / rib eye", @"OJO( DE)? BIFE|\bOJO\b|RIB ?-?EYE|BIFE ANCHO|ANCHO\b|ENTRECOTE|BIFE ANCHO|DELMONICO"),
        R(@"CARNES VACUNAS", @"Entraña / skirt", @"ENTRANI?T?A|SKIRT|AMERICANA \d|^AMERICANA"),
        R(@"CARNES VACUNAS", @"Vacío / flap", @"VACIO|FLAP|BAVETTE|FRALDINHA"),
        R(@"CARNES VACUNAS", @"Lomo / filet mignon", @"\bLOMO\b|FILET|FILETE|SOLOMILLO|MEDALLON|TENDERLOIN|CHATEAUBRIAND|FILE MIGNON|MIGNON"),
        R(@"CARNES VACUNAS", @"Picanha", @"PICAN(C?H)?A|PICANA|COULOTTE|TAPA DE CUADRIL|PUNTA DE ANCA"),
        R(@"CARNES VACUNAS", @"Asado de tira / short ribs", @"\bASADO|SHORT ?RIB|\bTIRA\b|COSTILLA DE (RES|VACA|TERNERA)|COSTELA|BANDERITA"),
        R(@"CERDO", @"Cerdo", @"CERDO|PORK|BONDIOLA|SECRETO|PLUMA IBERICA|PRESA IBERICA|\bRIBS\b|COSTILLITA|MATAMBRITO|LECHON|PORCO|COSTILLA|CARRE|PANCETA|BACON|TOCINO"),
        R(@"POLLO", @"Pollo", @"POLLO|POLLITO|CHICKEN|PECHUGA|SUPREMA|FRANGO|GALLINA|PATO|DUCK"),
        R(@"CORDERO", @"Cordero", @"CORDERO|\bLAMB\b|PALETILLA|CABRITO|CORDEIRO|CARRE DE"),
        R(@"PESCADOS Y MARISCOS", @"Salmón", @"SALMON"),
        R(@"PESCADOS Y MARISCOS", @"Pulpo", @"PULPO|OCTOPUS|POLVO"),
        R(@"PESCADOS Y MARISCOS", @"Langostinos / camarones / langosta", @"LANGOST|GAMB|CAMARON|SHRIMP|PRAWN|LOBSTER|CAMARAO|SCAMPI|CANGREJO|CRAB|VIEIRA|SCALLOP"),
        R(@"PESCADOS Y MARISCOS", @"Otros pescados y mariscos", @"BRANZINO|SEA ?BASS|LUBINA|CORVINA|PESCA|\bFISH\b|TRUCHA|ATUN\b|\bTUNA\b|MERLUZA|CALAMAR|MARISC|CEVICHE|TIRADITO|ROBALO|DORADO|LENGUADO|BACALAO|MERO|PEIXE|MEJILLON|ALMEJA|OSTRA|OYSTER|SEAFOOD|CONGRIO|REINETA|SALMON"),
        R(@"CARNES VACUNAS", @"Otros cortes vacunos", @"PUNTA DE GANSO|MATAMBRE|PECETO|CUADRIL|BOLA DE LOMO|NALGA|TRI-?TIP|HANGER|WAGYU|BIFE\b|\bSTEAK\b|PARRILLADA|CARNE|BEEF|ANGUS|\bCORTE\b|PARRILLA PARA|MIXTA PARRILL|GRILL MIX|\bPRIME\b|BRISKET|CHURRASCO|ROAST|A LO POBRE"),
        R(@"MILANESAS", @"Milanesa", @"NAPOLITAN|NAPO\b"),
        R(@"GUARNICIONES", @"Papas fritas", @"PAPAS? FRITAS?|FRENCH FRIES|\bFRIES\b|FRITAS|BATATA FRITA|PATATAS FRITAS|PAPAS (BASTON|PAJA|PROVENZAL|TRUFADAS|CON CHEDDAR)|PAPA FRITA|FRITAS"),
        R(@"GUARNICIONES", @"Puré", @"PURE|MASHED|MOUSSELINE"),
        R(@"GUARNICIONES", @"Otras papas", @"\bPAPAS?\b|POTATO|PATATA|BATATA|BONIATO|CASCOS|RUSTICA|GRATIN|ROSTI|PAPA AL|DAUPHIN"),
        R(@"GUARNICIONES", @"Vegetales", @"VERDURA|VEGETAL|VEGETABLE|VEGETARIAN|VEGAN|VEGET|ESPARRAG|ESPAARRAG|ASPARAGUS|BROCOLI|BROCCOLI|ZANAHORIA|HONGOS|CHAMPI|SETAS|MUSHROOM|CEBOLLA|CALABAZA|ZAPALLO|CHOCLO|LEGUMES|PIMIENTO|BERENJENA|ESPINACA|SPINACH|HUMITA|TOMATE|BROCHET|BROCHETA|GRILLED VEG|CREAMED|MAIZ|CORN|COLIFLOR|BRUSSELS|REPOLLO|PALTA|AGUACATE|AVOCADO|TARTAR"),
        R(@"GUARNICIONES", @"Arroz", @"ARROZ|RICE|RISOTTO|BIRO"),
        R(@"GUARNICIONES", @"Mac & cheese / otras guarniciones", @"MAC ?& ?CHEESE|MAC AND CHEESE|GUARN|\bSIDE\b|ACOMPANAMIENTO|FAROFA|MANDIOCA|YUCA|PATACON|PLATANO|MADURO|FEIJAO|POLENTA|HUEVO|EGG"),
        R(@"ENTRADAS", @"Burrata y quesos", @"BURRATA|QUESO|MANCHEGO|CHEESE(?!CAKE)|QUEIJO|MOZZ|FONDUE|BRIE|CAMEMBERT"),
        R(@"ENTRADAS", @"Jamón y fiambres", @"JAMON|SERRANO|BELLOTA|IBERICO|PATA NEGRA|CHARCUT|FIAMBRE|PROSCIUTTO|SALAME|PICADA|TABLA"),
        R(@"POSTRES", @"Panqueques / crepes", @"PANQUEQUE|PANCAKE|CREPE|PANQUECA"),
        R(@"POSTRES", @"Flan", @"\bFLAN\b|PUDDIN|BUDIN|QUESILLO|PUDIM"),
        R(@"POSTRES", @"Helados", @"\bHELADO|ICE ?CREAM|GELATO|SORBETE|SORVETE|BOCHA|SUNDAE|AFFOGATO"),
        R(@"POSTRES", @"Volcán / chocolate", @"VOLCAN|LAVA|BROWNIE|CHOCOLATE|FONDANT|MARQUISE"),
        R(@"POSTRES", @"Otros postres", @"BRULEE?|CHAJA|DEGUSTACION POSTRES|DESGUSTACION|TIRAMISU|TORTA|CAKE|CHEESECAKE|MOUSSE|ALFAJOR|DULCE DE LECHE|POSTRE|DESSERT|PANNA ?COTTA|PAVLOVA|TARTA|TARTE|FRUTILLA|FRUTA|MERENGUE|BABA|SUSPIRO|TRES LECHES|CHURRO|BANANA SPLIT|COPA HELADA|SOUFFLE(?! GNOCCHI)|SOPAIPILLA|ARROZ CON LECHE|PAPAYA|BOMBON|MAZAPAN|PETIT FOUR|MACARON|COOKIE|GALLETA"),
        R(@"PANADERÍA Y PASTELERÍA", @"Panadería y pastelería", @"MEDIALUNA|CROISSANT|ALMOJABANA|PAN DE BONO|PAN DE QUESO|BUNUELO|FACTURA(?! A)|MUFFIN|\bSCON|\bDONA\b|DONUT|BUDIN|MAGDALENA|CHIPA|PASTEL|PASTELERIA|BIZCOCHO|ALFAJOR|TOSTADA|TOSTADAS|PAN\b|BREAD|FOCACCIA|GRISIN|BAGEL"),
        R(@"SERVICIO DE MESA", @"Cubierto / pan y salsas", @"CUBIERTO|COUVERT|SALSA|SAUCE|CHIMICHURRI|MANTECA|BUTTER|ADEREZO|DIP\b|CRIOLLA|ALIOLI|MAYO"),
        R(@"OTROS", @"Menú infantil", @"KIDS|NINO|INFANTIL|CHILDREN"),
        R(@"OTROS", @"Golosinas / almacén", @"GOLOSINA|SNACK|CHICLE|CARAMELO|ALFAJOR|CHOCOLATIN|PAPAS LAYS|LAYS|PRINGLES|BARRITA|TURRON|ALMACEN"),
        R(@"OTROS", @"Menú ejecutivo / combos varios", @"MENU (DEL DIA|EJECUTIVO)|EJECUTIVO|\bCOMBO\b|PROMO|LUNCH SPECIAL|PRIX FIXE|DEGUSTACION|TASTING"),
    };

    // Respaldo cuando el nombre no matchea: categoría normalizada del POS o familia.
    private static readonly Dictionary<string, (string Rubro, string Producto)> RespaldoCategoria = new()
    {
        ["WINE"] = ("VINO", "Vino por botella"),
        ["COCKTAIL"] = ("COCTELERÍA Y DESTILADOS", "Cócteles de autor y otros"),
        ["BEVERAGE"] = ("BEBIDAS SIN ALCOHOL", "Otras bebidas sin alcohol"),
        ["DESSERT"] = ("POSTRES", "Otros postres"),
        ["COFFEE"] = ("CAFÉ E INFUSIONES", "Café"),
    };

    private static readonly (Regex Patron, string Rubro, string Producto)[] RespaldoFamilia =
    {
        (new Regex("LICOR|TRAGO|COCTEL|COCKTAIL|BAR", Op), "COCTELERÍA Y DESTILADOS", "Cócteles de autor y otros"),
        (new Regex("VINO|WINE", Op), "VINO", "Vino por botella"),
        (new Regex("CERVEZ|BEER", Op), "CERVEZA", "Cerveza"),
    };

    private static readonly Regex Espacios = new(@"\s+", Op);
    private static readonly Regex Combo = new(@"\+|\bCOMBO\b|\bMENU\b", Op);
    private static readonly Regex Media = new(@"^1/2|\b1/2\b|MEDIA PORCION|\bMEDIO\b|\bHALF\b", Op);
    private static readonly Regex Wagyu = new(@"WAGYU|WUAYU|WAYU", Op);
    private static readonly Regex DryAged = new(@"DRY ?AGE|MADURAD", Op);
    private static readonly Regex Prime = new(@"\bPRIME\b", Op);
    private static readonly Regex Desayuno = new(@"^(INF|INFUSION|CAFE)\b\s*\+", Op);
    private static readonly Regex Peso = new(@"(\d{2,4})\s*(GRS?|GMS?|GR\.|G\b|GRAMOS|KG|OZ)", Op);
    private static readonly Regex UnKg = new(@"\b1 ?KG\b", Op);
    private static readonly Regex UnidadesEmpanada = new(@"X ?(\d)\b|\((\d) ?UN|(\d) ?U\b|(\d) UNIDAD|(\d)UN\b", Op);

    /// <summary>Mayúsculas, sin acentos y con los caracteres rotos de algunos POS arreglados (ENTRA±A → ENTRAÑA).</summary>
    public static string Normalizar(string? nombre)
    {
        var s = (nombre ?? "").Replace("3⁄4", "O").Replace("¾", "O").Replace("±", "Ñ").Replace("Ð", "Ñ")
            .Replace("“", "").Replace("”", "");
        s = s.ToUpperInvariant().Normalize(NormalizationForm.FormKD);
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return Espacios.Replace(sb.ToString(), " ").Trim();
    }

    public static ProductoClasificado Clasificar(string? nombre, string? categoria = null, string? familia = null)
    {
        var n = Normalizar(nombre);
        var combo = Combo.IsMatch(n);
        var media = Media.IsMatch(n);
        var premium = Wagyu.IsMatch(n) ? "WAGYU" : DryAged.IsMatch(n) ? "DRY AGED" : Prime.IsMatch(n) ? "PRIME" : "";
        var principal = n.Split('+')[0].Trim();
        if (principal.Length == 0 || Desayuno.IsMatch(n)) principal = n; // desayunos: el combo es el producto
        var gramos = Gramos(n);

        string? rubro = null, producto = null;
        foreach (var r in Reglas)
        {
            if (!r.Patron.IsMatch(principal)) continue;
            rubro = r.Rubro; producto = r.Producto;
            break;
        }
        if (rubro is null)
        {
            var fam = Normalizar(familia);
            foreach (var (patron, ru, pr) in RespaldoFamilia)
            {
                if (!patron.IsMatch(fam)) continue;
                rubro = ru; producto = pr;
                break;
            }
            if (rubro is null && categoria is not null && RespaldoCategoria.TryGetValue(categoria, out var v))
                (rubro, producto) = v;
        }
        rubro ??= RubroSinClasificar;
        producto ??= ProductoSinClasificar;

        int? unidades = null;
        if (producto == "Empanadas")
        {
            var m = UnidadesEmpanada.Match(n);
            if (m.Success)
                for (var i = 1; i < m.Groups.Count; i++)
                    if (m.Groups[i].Success) { unidades = int.Parse(m.Groups[i].Value, CultureInfo.InvariantCulture); break; }
        }
        return new ProductoClasificado(rubro, producto, combo, media, premium, gramos, unidades);
    }

    /// <summary>Gramos de la porción cuando el nombre los trae (400GR, 14 OZ, 1 KG); null si no o si quedan fuera de 80–2000 g.</summary>
    public static int? Gramos(string n)
    {
        var m = Peso.Match(n);
        if (!m.Success) return UnKg.IsMatch(n) ? 1000 : null;
        var v = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        var u = m.Groups[2].Value;
        if (u.StartsWith("OZ", StringComparison.Ordinal)) v = Math.Round(v * 28.35);
        if (u == "KG") v *= 1000;
        return v is >= 80 and <= 2000 ? (int)v : null;
    }
}
