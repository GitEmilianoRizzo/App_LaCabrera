using FluentValidation;
using LaCabrera.Api.Models.DTOs;

namespace LaCabrera.Api.Validators;

public class DailySalesBatchRequestValidator : AbstractValidator<DailySalesBatchRequest>
{
    private static readonly string[] ValidStatuses = { "OPEN", "CLOSED", "CANCELLED", "VOIDED", "REFUNDED" };
    private static readonly string[] ValidCategories = { "STARTER", "MAIN_COURSE", "SIDE_DISH", "DESSERT", "COFFEE", "BEVERAGE", "WINE", "COCKTAIL", "OTHER" };
    private static readonly string[] ValidUploadTypes = { "FULL_DAY", "INCREMENTAL", "CORRECTION" };
    private static readonly string[] SupportedSchemaVersions = { "1.0", "1.1", "1.2" };

    public DailySalesBatchRequestValidator()
    {
        RuleFor(x => x.SchemaVersion)
            .NotEmpty().WithMessage("schema_version es requerido")
            .Must(v => SupportedSchemaVersions.Contains(v))
            .WithMessage($"schema_version debe ser uno de: {string.Join(", ", SupportedSchemaVersions)}");

        RuleFor(x => x.BatchHeader)
            .NotNull().WithMessage("batch_header es requerido")
            .SetValidator(new BatchHeaderValidator());

        RuleFor(x => x.Tickets)
            .NotNull().WithMessage("tickets es requerido");

        RuleForEach(x => x.Tickets)
            .SetValidator(new TicketValidator());
    }
}

public class BatchHeaderValidator : AbstractValidator<BatchHeader>
{
    public BatchHeaderValidator()
    {
        RuleFor(x => x.BatchId)
            .NotEmpty().WithMessage("batch_header.batch_id es requerido")
            .MaximumLength(200).WithMessage("batch_header.batch_id no puede exceder 200 caracteres");

        RuleFor(x => x.BusinessDate)
            .NotEmpty().WithMessage("batch_header.business_date es requerido")
            .Must(BeValidDate).WithMessage("batch_header.business_date debe ser una fecha valida (YYYY-MM-DD)");

        RuleFor(x => x.GeneratedAt)
            .NotEmpty().WithMessage("batch_header.generated_at es requerido")
            .Must(BeValidDateTime).WithMessage("batch_header.generated_at debe ser un datetime ISO 8601 valido");

        RuleFor(x => x.Franchise)
            .NotNull().WithMessage("batch_header.franchise es requerido")
            .SetValidator(new FranchiseInfoValidator());

        RuleFor(x => x.ControlTotals)
            .NotNull().WithMessage("batch_header.control_totals es requerido")
            .SetValidator(new ControlTotalsValidator());

        RuleFor(x => x.UploadType)
            .Must(x => string.IsNullOrEmpty(x) || new[] { "FULL_DAY", "INCREMENTAL", "CORRECTION" }.Contains(x))
            .WithMessage("batch_header.upload_type debe ser FULL_DAY, INCREMENTAL o CORRECTION");
    }

    private bool BeValidDate(string date)
    {
        return DateTime.TryParse(date, out _);
    }

    private bool BeValidDateTime(string dateTime)
    {
        return DateTimeOffset.TryParse(dateTime, out _);
    }
}

public class FranchiseInfoValidator : AbstractValidator<FranchiseInfo>
{
    public FranchiseInfoValidator()
    {
        RuleFor(x => x.FranchiseCode)
            .NotEmpty().WithMessage("batch_header.franchise.franchise_code es requerido")
            .MaximumLength(50).WithMessage("batch_header.franchise.franchise_code no puede exceder 50 caracteres")
            .Matches(@"^[A-Za-z0-9_]+$").WithMessage("batch_header.franchise.franchise_code solo puede contener letras, numeros y guion bajo");

        RuleFor(x => x.Timezone)
            .NotEmpty().WithMessage("batch_header.franchise.timezone es requerido");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("batch_header.franchise.currency es requerido")
            .Matches(@"^[A-Z]{3}$").WithMessage("batch_header.franchise.currency debe ser un codigo ISO 4217 (3 letras mayusculas)");
    }
}

public class ControlTotalsValidator : AbstractValidator<ControlTotals>
{
    public ControlTotalsValidator()
    {
        RuleFor(x => x.TicketCount)
            .GreaterThanOrEqualTo(0).WithMessage("batch_header.control_totals.ticket_count debe ser >= 0");

        RuleFor(x => x.ItemLineCount)
            .GreaterThanOrEqualTo(0).WithMessage("batch_header.control_totals.item_line_count debe ser >= 0");
    }
}

public class TicketValidator : AbstractValidator<TicketDto>
{
    private static readonly string[] ValidStatuses = { "OPEN", "CLOSED", "CANCELLED", "VOIDED", "REFUNDED" };

    public TicketValidator()
    {
        RuleFor(x => x.TicketId)
            .NotEmpty().WithMessage("ticket.ticket_id es requerido")
            .MaximumLength(200).WithMessage("ticket.ticket_id no puede exceder 200 caracteres");

        RuleFor(x => x.TicketNumber)
            .NotEmpty().WithMessage("ticket.ticket_number es requerido");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("ticket.status es requerido")
            .Must(x => ValidStatuses.Contains(x)).WithMessage("ticket.status debe ser OPEN, CLOSED, CANCELLED, VOIDED o REFUNDED");

        RuleFor(x => x.OpenedAt)
            .NotEmpty().WithMessage("ticket.opened_at es requerido")
            .Must(BeValidDateTime).WithMessage("ticket.opened_at debe ser un datetime ISO 8601 valido");

        RuleFor(x => x.ClosedAt)
            .Must(BeValidDateTimeOrNull).WithMessage("ticket.closed_at debe ser un datetime ISO 8601 valido");

        RuleFor(x => x.BusinessDate)
            .NotEmpty().WithMessage("ticket.business_date es requerido")
            .Must(BeValidDate).WithMessage("ticket.business_date debe ser una fecha valida (YYYY-MM-DD)");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("ticket.currency es requerido")
            .Matches(@"^[A-Z]{3}$").WithMessage("ticket.currency debe ser un codigo ISO 4217");

        RuleFor(x => x.Amounts)
            .NotNull().WithMessage("ticket.amounts es requerido")
            .SetValidator(new TicketAmountsValidator());

        RuleFor(x => x.Items)
            .NotNull().WithMessage("ticket.items es requerido");

        RuleForEach(x => x.Items)
            .SetValidator(new TicketItemValidator());
    }

    private bool BeValidDate(string date)
    {
        return DateTime.TryParse(date, out _);
    }

    private bool BeValidDateTime(string dateTime)
    {
        return DateTimeOffset.TryParse(dateTime, out _);
    }

    private bool BeValidDateTimeOrNull(string? dateTime)
    {
        return string.IsNullOrEmpty(dateTime) || DateTimeOffset.TryParse(dateTime, out _);
    }
}

public class TicketAmountsValidator : AbstractValidator<TicketAmounts>
{
    public TicketAmountsValidator()
    {
        RuleFor(x => x.GrossAmount)
            .GreaterThanOrEqualTo(0).WithMessage("ticket.amounts.gross_amount debe ser >= 0");

        RuleFor(x => x.DiscountAmount)
            .GreaterThanOrEqualTo(0).WithMessage("ticket.amounts.discount_amount debe ser >= 0");
    }
}

public class TicketItemValidator : AbstractValidator<TicketItemDto>
{
    private static readonly string[] ValidCategories = { "STARTER", "MAIN_COURSE", "SIDE_DISH", "DESSERT", "COFFEE", "BEVERAGE", "WINE", "COCKTAIL", "OTHER" };

    public TicketItemValidator()
    {
        RuleFor(x => x.LineId)
            .NotEmpty().WithMessage("item.line_id es requerido");

        RuleFor(x => x.ProductCode)
            .NotEmpty().WithMessage("item.product_code es requerido");

        RuleFor(x => x.ProductName)
            .NotEmpty().WithMessage("item.product_name es requerido");

        RuleFor(x => x.ProductCategory)
            .NotEmpty().WithMessage("item.product_category es requerido")
            .Must(x => ValidCategories.Contains(x)).WithMessage("item.product_category debe ser STARTER, MAIN_COURSE, SIDE_DISH, DESSERT, COFFEE, BEVERAGE, WINE, COCKTAIL u OTHER");

        RuleFor(x => x.Quantity)
            .GreaterThanOrEqualTo(0).WithMessage("item.quantity debe ser >= 0");

        RuleFor(x => x.UnitPrice)
            .GreaterThanOrEqualTo(0).WithMessage("item.unit_price debe ser >= 0");
    }
}
