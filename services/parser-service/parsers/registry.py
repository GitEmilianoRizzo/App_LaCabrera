"""
Parser Registry - Manages available parsers
"""
from typing import Dict, Optional, List
from .base_parser import BaseParser
from .toast_parser import ToastParserImpl
from .toast_html_parser import ToastHtmlParserImpl
from .colombia_json_parser import ColombiaJsonParserImpl


class ParserRegistry:
    """Registry of available parsers"""

    _parsers: Dict[str, BaseParser] = {}

    @classmethod
    def register(cls, parser: BaseParser):
        """Register a parser"""
        cls._parsers[parser.code] = parser

    @classmethod
    def get(cls, code: str) -> Optional[BaseParser]:
        """Get a parser by code"""
        return cls._parsers.get(code)

    @classmethod
    def list_all(cls) -> List[Dict]:
        """List all registered parsers"""
        return [
            {
                "code": p.code,
                "name": p.name,
                "extensions": p.extensions
            }
            for p in cls._parsers.values()
        ]

    @classmethod
    def initialize(cls):
        """Initialize and register all parsers"""
        cls.register(ToastParserImpl())
        cls.register(ToastHtmlParserImpl())
        cls.register(ColombiaJsonParserImpl())


# Initialize registry on import
ParserRegistry.initialize()
