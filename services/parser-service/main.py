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
    version="1.1.0"
)

# CORS - Allow backend to call this service
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


class ParserInfo(BaseModel):
    code: str
    name: str
    extensions: List[str]


class ParsePreview(BaseModel):
    franchise_code: Optional[str] = None
    franchise_name: Optional[str] = None
    business_date: Optional[str] = None
    ticket_count: int = 0
    net_sales_amount: float = 0
    tax_amount: float = 0
    currency: str = "USD"


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


@app.get("/health", response_model=HealthResponse)
async def health_check():
    return HealthResponse(status="healthy", service="parser-service", version="1.1.0")


@app.get("/parsers", response_model=List[ParserInfo])
async def list_parsers():
    return ParserRegistry.list_all()


@app.post("/parse", response_model=ParseFileResult)
async def parse_file(file: UploadFile = File(...), parser_code: str = Form(...)):
    parser = ParserRegistry.get(parser_code)
    if not parser:
        raise HTTPException(status_code=400, detail=f"Parser not found: {parser_code}")

    filename = file.filename or "unknown.txt"
    ext = "." + filename.rsplit(".", 1)[-1].lower() if "." in filename else ""
    if ext not in parser.extensions:
        return ParseFileResult(success=False, filename=filename,
            error=f"Invalid file extension. Parser {parser_code} supports: {parser.extensions}")

    try:
        content = await file.read()
        content_str = content.decode("utf-8", errors="ignore")
        result = parser.parse(content_str, filename)
        if result.success:
            preview = ParsePreview(**result.preview) if result.preview else None
            return ParseFileResult(success=True, filename=filename, preview=preview, data=result.data)
        else:
            return ParseFileResult(success=False, filename=filename, error=result.error)
    except Exception as e:
        return ParseFileResult(success=False, filename=filename, error=f"Error: {str(e)}")


@app.post("/parse-batch", response_model=ParseBatchResult)
async def parse_batch(files: List[UploadFile] = File(...), parser_code: str = Form(...)):
    parser = ParserRegistry.get(parser_code)
    if not parser:
        raise HTTPException(status_code=400, detail=f"Parser not found: {parser_code}")

    results = []
    successful = 0
    failed = 0

    for file in files:
        filename = file.filename or "unknown.txt"
        ext = "." + filename.rsplit(".", 1)[-1].lower() if "." in filename else ""
        if ext not in parser.extensions:
            results.append(ParseFileResult(success=False, filename=filename, error=f"Invalid extension"))
            failed += 1
            continue
        try:
            content = await file.read()
            content_str = content.decode("utf-8", errors="ignore")
            result = parser.parse(content_str, filename)
            if result.success:
                preview = ParsePreview(**result.preview) if result.preview else None
                results.append(ParseFileResult(success=True, filename=filename, preview=preview, data=result.data))
                successful += 1
            else:
                results.append(ParseFileResult(success=False, filename=filename, error=result.error))
                failed += 1
        except Exception as e:
            results.append(ParseFileResult(success=False, filename=filename, error=f"Error: {str(e)}"))
            failed += 1

    return ParseBatchResult(total_files=len(files), successful=successful, failed=failed, results=results)


@app.post("/parse-toast-combined", response_model=ParseFileResult)
async def parse_toast_combined(
    html_file: UploadFile = File(..., description="HTML Order Details file"),
    csv_zip: UploadFile = File(None, description="Optional ZIP with CSV summaries")
):
    """Parse Toast with HTML + CSV validation."""
    from parsers.toast_combined_parser import ToastCombinedParserImpl

    parser = ToastCombinedParserImpl()
    filename = html_file.filename or "unknown.html"

    ext = "." + filename.rsplit(".", 1)[-1].lower() if "." in filename else ""
    if ext not in [".html", ".htm"]:
        return ParseFileResult(success=False, filename=filename, error=f"Must be .html or .htm")

    try:
        html_content = await html_file.read()
        html_str = html_content.decode("utf-8", errors="ignore")

        csv_zip_content = None
        if csv_zip and csv_zip.filename:
            csv_zip_content = await csv_zip.read()

        result = parser.parse(html_str, filename, csv_zip_content=csv_zip_content)

        if result.success:
            preview = ParsePreview(**result.preview) if result.preview else None
            return ParseFileResult(success=True, filename=filename, preview=preview, data=result.data)
        else:
            return ParseFileResult(success=False, filename=filename, error=result.error)
    except Exception as e:
        return ParseFileResult(success=False, filename=filename, error=f"Error: {str(e)}")


if __name__ == "__main__":
    uvicorn.run("main:app", host="0.0.0.0", port=8000, reload=True)
