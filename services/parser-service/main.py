"""
Parser Service - FastAPI Microservice
Procesa archivos TXT con parsers Python y devuelve JSON para ingesta.
"""
from fastapi import FastAPI, UploadFile, File, HTTPException, Form
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from typing import List, Optional, Dict, Any
import uvicorn

from parsers.registry import ParserRegistry


app = FastAPI(
    title="Parser Service",
    description="Microservicio para parsear archivos de diferentes POS systems",
    version="1.0.0"
)

# CORS - Allow backend to call this service
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # In production, restrict to backend URL
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


# ============================================================================
# Response Models
# ============================================================================

class ParserInfo(BaseModel):
    code: str
    name: str
    extensions: List[str]


class ParsePreview(BaseModel):
    franchise_code: Optional[str]
    franchise_name: Optional[str]
    business_date: Optional[str]
    ticket_count: int
    net_sales_amount: float
    tax_amount: float
    currency: str


class ParseFileResult(BaseModel):
    success: bool
    filename: str
    preview: Optional[ParsePreview] = None
    data: Optional[Dict[str, Any]] = None
    error: Optional[str] = None


class ParseBatchResult(BaseModel):
    total_files: int
    successful: int
    failed: int
    results: List[ParseFileResult]


class HealthResponse(BaseModel):
    status: str
    service: str
    version: str


# ============================================================================
# Endpoints
# ============================================================================

@app.get("/health", response_model=HealthResponse)
async def health_check():
    """Health check endpoint"""
    return HealthResponse(
        status="healthy",
        service="parser-service",
        version="1.0.0"
    )


@app.get("/parsers", response_model=List[ParserInfo])
async def list_parsers():
    """List all available parsers"""
    return ParserRegistry.list_all()


@app.post("/parse", response_model=ParseFileResult)
async def parse_file(
    file: UploadFile = File(...),
    parser_code: str = Form(...)
):
    """
    Parse a single file with the specified parser.

    - **file**: The file to parse (typically .txt)
    - **parser_code**: Code of the parser to use (e.g., 'TOAST_PARSER')

    Returns the parsed data and a preview summary.
    """
    # Get parser
    parser = ParserRegistry.get(parser_code)
    if not parser:
        raise HTTPException(
            status_code=400,
            detail=f"Parser not found: {parser_code}"
        )

    # Check extension
    filename = file.filename or "unknown.txt"
    ext = "." + filename.rsplit(".", 1)[-1].lower() if "." in filename else ""
    if ext not in parser.extensions:
        return ParseFileResult(
            success=False,
            filename=filename,
            error=f"Invalid file extension '{ext}'. Parser {parser_code} supports: {parser.extensions}"
        )

    try:
        # Read content
        content = await file.read()
        content_str = content.decode('utf-8', errors='ignore')

        # Parse
        result = parser.parse(content_str, filename)

        if result.success:
            preview = ParsePreview(**result.preview) if result.preview else None
            return ParseFileResult(
                success=True,
                filename=filename,
                preview=preview,
                data=result.data
            )
        else:
            return ParseFileResult(
                success=False,
                filename=filename,
                error=result.error
            )

    except Exception as e:
        return ParseFileResult(
            success=False,
            filename=filename,
            error=f"Error processing file: {str(e)}"
        )


@app.post("/parse-batch", response_model=ParseBatchResult)
async def parse_batch(
    files: List[UploadFile] = File(...),
    parser_code: str = Form(...)
):
    """
    Parse multiple files with the specified parser.

    - **files**: List of files to parse
    - **parser_code**: Code of the parser to use

    Returns results for each file. Processing continues even if some files fail.
    """
    parser = ParserRegistry.get(parser_code)
    if not parser:
        raise HTTPException(
            status_code=400,
            detail=f"Parser not found: {parser_code}"
        )

    results: List[ParseFileResult] = []
    successful = 0
    failed = 0

    for file in files:
        filename = file.filename or "unknown.txt"
        ext = "." + filename.rsplit(".", 1)[-1].lower() if "." in filename else ""

        if ext not in parser.extensions:
            results.append(ParseFileResult(
                success=False,
                filename=filename,
                error=f"Invalid extension '{ext}'. Supports: {parser.extensions}"
            ))
            failed += 1
            continue

        try:
            content = await file.read()
            content_str = content.decode('utf-8', errors='ignore')

            result = parser.parse(content_str, filename)

            if result.success:
                preview = ParsePreview(**result.preview) if result.preview else None
                results.append(ParseFileResult(
                    success=True,
                    filename=filename,
                    preview=preview,
                    data=result.data
                ))
                successful += 1
            else:
                results.append(ParseFileResult(
                    success=False,
                    filename=filename,
                    error=result.error
                ))
                failed += 1

        except Exception as e:
            results.append(ParseFileResult(
                success=False,
                filename=filename,
                error=f"Error: {str(e)}"
            ))
            failed += 1

    return ParseBatchResult(
        total_files=len(files),
        successful=successful,
        failed=failed,
        results=results
    )


# ============================================================================
# Main
# ============================================================================

if __name__ == "__main__":
    uvicorn.run(
        "main:app",
        host="0.0.0.0",
        port=8000,
        reload=True
    )
