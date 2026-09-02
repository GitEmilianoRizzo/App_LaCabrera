"""
Base Parser - Abstract class for all parsers
"""
from abc import ABC, abstractmethod
from typing import Dict, Any, Optional
from dataclasses import dataclass

@dataclass
class ParseResult:
    """Result of parsing a file"""
    success: bool
    data: Optional[Dict[str, Any]] = None
    error: Optional[str] = None
    preview: Optional[Dict[str, Any]] = None

    @classmethod
    def ok(cls, data: Dict[str, Any], preview: Dict[str, Any] = None) -> 'ParseResult':
        return cls(success=True, data=data, preview=preview)

    @classmethod
    def fail(cls, error: str) -> 'ParseResult':
        return cls(success=False, error=error)


class BaseParser(ABC):
    """Abstract base class for all file parsers"""

    @property
    @abstractmethod
    def code(self) -> str:
        """Unique parser code (e.g., 'TOAST_PARSER')"""
        pass

    @property
    @abstractmethod
    def name(self) -> str:
        """Human-readable parser name"""
        pass

    @property
    @abstractmethod
    def extensions(self) -> list:
        """Supported file extensions (e.g., ['.txt'])"""
        pass

    @abstractmethod
    def parse(self, content: str, filename: str = "") -> ParseResult:
        """
        Parse file content and return result.

        Args:
            content: The file content as string
            filename: Original filename (for context)

        Returns:
            ParseResult with success status, parsed data, and preview
        """
        pass

    def generate_preview(self, data: Dict[str, Any]) -> Dict[str, Any]:
        """
        Generate a preview summary from parsed data.
        Override in subclasses for custom previews.
        """
        header = data.get('batch_header', {})
        control = header.get('control_totals', {})
        franchise = header.get('franchise', {})

        return {
            "franchise_code": franchise.get('franchise_code'),
            "franchise_name": franchise.get('franchise_name'),
            "business_date": header.get('business_date'),
            "ticket_count": control.get('ticket_count', 0),
            "net_sales_amount": control.get('net_sales_amount', 0),
            "tax_amount": control.get('tax_amount', 0),
            "currency": franchise.get('currency', 'USD')
        }
