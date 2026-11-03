"""
Categoría de producto para los parsers de Toast (Miami), a partir del nombre del ítem.

Una sola regla para el parser HTML y el combinado, así la categoría de un mismo producto no cambia
según el parser con el que se cargó. Se compara por palabra completa: "outside skirt" no es
"side", y las carnes se evalúan antes que las guarniciones.

Si se cambia esta regla, actualizar también la migración que recategoriza los datos de Miami
(database/migrations/033_fix_miami_toast_neto_y_categorias.sql).
"""
import re
import unicodedata

# Orden de evaluación: la primera categoría que coincide gana (guarnición antes que entrada: "side salad").
REGLAS = [
    ('COFFEE', ['coffee', 'cafe', 'espresso', 'cappuccino', 'latte', 'cortado', 'macchiato']),
    ('WINE', ['wine', 'vino', 'malbec', 'cabernet', 'merlot', 'chardonnay', 'prosecco', 'champagne',
              'sauvignon', 'pinot', 'rose', 'blend', 'brut', 'torrontes', 'mb', 'cs', 'cf', 'catena', 'rutini',
              'enemigo', 'perdices', 'zuccardi', 'opus', 'reserva', 'zapata', 'encuentro', 'petit fleur',
              'cab', 'tempranillo', 'brunello', 'montalcino', 'descorche']),
    ('COCKTAIL', ['cocktail', 'mojito', 'margarita', 'martini', 'whisky', 'whiskey', 'gin', 'vodka',
                  'tequila', 'negroni', 'spritz', 'old fashioned', 'fernet', 'caipirinha', 'mimosa', 'sangria',
                  'sour', 'tito s', 'titos', 'grey goose', 'aperol', 'sophia loren', 'rum', 'ron', 'mezcal',
                  'virgin', 'mocktail', 'aguardiente', 'woodford', 'buchanans', 'hendricks', 'ketel', 'don julio', 'manhattan', 'americano', 'carajillo']),
    ('BEVERAGE', ['coke', 'coca', 'sprite', 'fanta', 'agua', 'water', 'soda', 'juice', 'jugo', 'lemonade',
                  'limonada', 'pellegrino', 'panna', 'beer', 'cerveza', 'ipa', 'lager', 'tea', 'te', 'quilmes',
                  'stella', 'artois', 'corona', 'heineken', 'michelob', 'modelo', 'ginger beer', 'gingerbeer']),
    ('DESSERT', ['flan', 'dulce', 'helado', 'ice cream', 'torta', 'cake', 'brownie', 'alfajor', 'postre',
                 'dessert', 'panqueque', 'panqueques', 'pancake', 'pancakes', 'volcan', 'tiramisu', 'cheesecake',
                 'creme brulee', 'brulee']),
    ('MAIN_COURSE', ['bife', 'entrana', 'skirt', 'ojo', 'ribeye', 'rib eye', 'steak', 'lomo', 'filet', 'mignon',
                     'asado', 'short ribs', 'short rib', 'vacio', 'new york', 'ny', 'cowboy', 'chuleton',
                     'tomahawk', 'dry aged', 'picanha', 'costilla', 'pollo', 'chicken',
                     'salmon', 'pasta', 'ravioli', 'sorrentinos', 'burger', 'hamburguesa', 'milanesa',
                     'nino', 'kids', 'infantil', 'parrillada', 'cordero', 'lamb', 'pork', 'cerdo', 'branzino',
                     'seabass', 'sea bass', 'gnocchi', 'noquis', 'penne', 'spaghetti', 'spaguetti', 'fettuccine']),
    ('SIDE_DISH', ['papas', 'fries', 'pure', 'mashed', 'vegetales', 'vegetables', 'side', 'warmies',
                   'calentitos', 'arroz', 'rice', 'esparragos', 'espaarragos', 'asparagus', 'broccoli', 'papa',
                   'rustica', 'mac cheese']),
    ('STARTER', ['empanada', 'empanadas', 'provoleta', 'chorizo', 'morcilla', 'ensalada', 'salad',
                 'chinchulines', 'molleja', 'mollejas', 'burrata', 'burrrata', 'entrada', 'carpaccio', 'tartare', 'croquetas', 'pulpo', 'octopus',
                 'langostinos', 'shrimp', 'shrimps', 'sweetbreads', 'blood sausage', 'rucula', 'arugula', 'ppt', 'avocado', 'avocato']),
]


def normalizar(nombre: str) -> str:
    """Minúsculas, sin acentos y con la puntuación como espacio: 'ENTRAÑA / OUTSIDE SKIRT' -> 'entrana outside skirt'."""
    sin_acentos = unicodedata.normalize('NFKD', nombre or '').encode('ascii', 'ignore').decode('ascii')
    return ' '.join(re.sub(r'[^a-z0-9]+', ' ', sin_acentos.lower()).split())


def categorizar(nombre: str) -> str:
    texto = f' {normalizar(nombre)} '
    for categoria, palabras in REGLAS:
        if any(f' {p} ' in texto for p in palabras):
            return categoria
    return 'OTHER'
