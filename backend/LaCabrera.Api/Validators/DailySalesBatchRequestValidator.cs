using FluentValidation;
using LaCabrera.Api.Models.DTOs;
using LaCabrera.Api.Services;

namespace LaCabrera.Api.Validators;

public class DailySalesBatchRequestValidator : AbstractValidator<DailySalesBatchRequest>
{
    private static readonly string[] ValidStatuses = { "OPEN", "CLOSED", "CANCELLED", "VOIDED", "REFUNDED" };
    private static readonly string[] ValidCategories = { "STARTER", "MAIN_COURSE", "SIDE_DISH", "DESSERT", "COFFEE", "BEVERAGE", "WINE", "COCKTAIL", "OTHER" };
    private static readonly string[] ValidUploadTypes = { "FULL_DAY", "INCREMENTAL", "CORRECTION" };
    private static readonly string[] SupportedSchemaVersions = { "1.0", "1.1", "1.2", DailySalesBatchV13.SchemaVersion };

    private static readonly TicketValidator TicketLegacy = new();
    private static readonly TicketValidatorV13 TicketV13 = new();

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

        // v1.0-v1.2 siguen con las reglas de siempre; v1.3 (T-380) suma devoluciones negativas y más controles
        RuleForEach(x => x.Tickets)
            .SetValidator((root, _) => DailySalesBatchV13.Es(root) ? TicketV13 : TicketLegacy);

        When(DailySalesBatchV13.Es, () =>
        {
            RuleFor(x => x.BatchHeader.AmountsIncludeTax)
                .NotNull().WithErrorCode("MISSING_AMOUNTS_INCLUDE_TAX")
                .WithMessage("batch_header.amounts_include_tax is required in schema 1.3");

            RuleForEach(x => x.Tickets)
                .Must((root, ticket) => ticket.BusinessDate == root.BatchHeader.BusinessDate)
                .WithErrorCode("BUSINESS_DATE_MISMATCH")
                .WithMessage((root, ticket) =>
                    $"ticket {ticket.TicketId}: business_date {ticket.BusinessDate} differs from batch_header.business_date {root.BatchHeader.BusinessDate} (one business date per batch)");

            RuleFor(x => x.Tickets)
                .Must(tickets => tickets.Select(t => t.TicketId).Distinct(StringComparer.Ordinal).Count() == tickets.Count)
                .WithErrorCode("DUPLICATE_TICKET_ID")
                .WithMessage((root, tickets) =>
                    "ticket_id repeated in the batch: " + string.Join(", ", tickets.GroupBy(t => t.TicketId).Where(g => g.Count() > 1).Select(g => g.Key)));
        });
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

    public TicketValidator() : this(validarSignoLegacy: true)
    {
    }

    /// <param name="validarSignoLegacy">v1.0-v1.2: gross y discount del ticket >= 0. v1.3 lo reemplaza por sus reglas de signo.</param>
    protected TicketValidator(bool validarSignoLegacy)
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
            .NotNull().WithMessage("ticket.amounts es requerido");

        if (validarSignoLegacy)
        {
            RuleFor(x => x.Amounts)
                .SetValidator(new TicketAmountsValidator());
        }

        RuleFor(x => x.Items)
            .NotNull().WithMessage("ticket.items es requerido");

        RuleForEach(x => x.Items)
            .SetValidator(validarSignoLegacy ? new TicketItemValidator() : new TicketItemValidatorV13());
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

    public TicketItemValidator() : this(validarCantidad: true)
    {
    }

    /// <param name="validarCantidad">v1.0-v1.2: quantity >= 0. En v1.3 el signo depende del estado del ticket (TicketValidatorV13).</param>
    protected TicketItemValidator(bool validarCantidad)
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

        if (validarCantidad)
        {
            RuleFor(x => x.Quantity)
                .GreaterThanOrEqualTo(0).WithMessage("item.quantity debe ser >= 0");
        }

        RuleFor(x => x.UnitPrice)
            .GreaterThanOrEqualTo(0).WithMessage("item.unit_price debe ser >= 0");
    }
}

/// <summary>v1.3: igual que TicketItemValidator sin la regla de signo de quantity.</summary>
public class TicketItemValidatorV13 : TicketItemValidator
{
    public TicketItemValidatorV13() : base(validarCantidad: false)
    {
    }
}

/// <summary>
/// v1.3 (T-380): mismas reglas que v1.0-v1.2 más el signo de los importes.
/// Solo un ticket REFUNDED puede traer importes y cantidades negativas (y tiene que referenciar la venta original);
/// cualquier otro estado los trae en positivo. unit_price siempre es positivo.
/// </summary>
public class TicketValidatorV13 : TicketValidator
{
    private const string CodigoNegativo = "NEGATIVE_AMOUNT_NOT_REFUNDED";
    private const string CodigoDevolucion = "INVALID_REFUND";

    public TicketValidatorV13() : base(validarSignoLegacy: false)
    {
        When(t => t.Amounts != null && t.Status == "REFUNDED", () =>
        {
            RuleFor(x => x.OriginalTicketId)
                .NotEmpty().WithErrorCode(CodigoDevolucion)
                .WithMessage(t => $"ticket {t.TicketId}: original_ticket_id is required when status is REFUNDED");

            RuleFor(x => x.Amounts.GrossAmount).LessThanOrEqualTo(0).WithErrorCode(CodigoDevolucion)
                .WithMessage(t => $"ticket {t.TicketId}: REFUNDED amounts.gross_amount must be <= 0");
            RuleFor(x => x.Amounts.DiscountAmount).LessThanOrEqualTo(0).WithErrorCode(CodigoDevolucion)
                .WithMessage(t => $"ticket {t.TicketId}: REFUNDED amounts.discount_amount must be <= 0");
            RuleFor(x => x.Amounts.NetAmount).LessThanOrEqualTo(0).WithErrorCode(CodigoDevolucion)
                .WithMessage(t => $"ticket {t.TicketId}: REFUNDED amounts.net_amount must be <= 0");

            RuleForEach(x => x.Items).ChildRules(item =>
            {
                item.RuleFor(i => i.Quantity).LessThanOrEqualTo(0).WithErrorCode(CodigoDevolucion)
                    .WithMessage(i => $"line {i.LineId}: REFUNDED quantity must be <= 0");
                item.RuleFor(i => i.GrossAmount).LessThanOrEqualTo(0).WithErrorCode(CodigoDevolucion)
                    .WithMessage(i => $"line {i.LineId}: REFUNDED gross_amount must be <= 0");
                item.RuleFor(i => i.NetAmount).LessThanOrEqualTo(0).WithErrorCode(CodigoDevolucion)
                    .WithMessage(i => $"line {i.LineId}: REFUNDED net_amount must be <= 0");
            });
        });

        When(t => t.Amounts != null && t.Status != "REFUNDED", () =>
        {
            RuleFor(x => x.Amounts.GrossAmount).GreaterThanOrEqualTo(0).WithErrorCode(CodigoNegativo)
                .WithMessage(t => $"ticket {t.TicketId}: negative amounts are only allowed when status is REFUNDED (amounts.gross_amount)");
            RuleFor(x => x.Amounts.DiscountAmount).GreaterThanOrEqualTo(0).WithErrorCode(CodigoNegativo)
                .WithMessage(t => $"ticket {t.TicketId}: negative amounts are only allowed when status is REFUNDED (amounts.discount_amount)");
            RuleFor(x => x.Amounts.NetAmount).GreaterThanOrEqualTo(0).WithErrorCode(CodigoNegativo)
                .WithMessage(t => $"ticket {t.TicketId}: negative amounts are only allowed when status is REFUNDED (amounts.net_amount)");

            RuleForEach(x => x.Items).ChildRules(item =>
            {
                item.RuleFor(i => i.Quantity).GreaterThanOrEqualTo(0).WithErrorCode(CodigoNegativo)
                    .WithMessage(i => $"line {i.LineId}: negative quantity is only allowed when status is REFUNDED");
                item.RuleFor(i => i.GrossAmount).GreaterThanOrEqualTo(0).WithErrorCode(CodigoNegativo)
                    .WithMessage(i => $"line {i.LineId}: negative gross_amount is only allowed when status is REFUNDED");
                item.RuleFor(i => i.DiscountAmount).GreaterThanOrEqualTo(0).WithErrorCode(CodigoNegativo)
                    .WithMessage(i => $"line {i.LineId}: negative discount_amount is only allowed when status is REFUNDED");
                item.RuleFor(i => i.NetAmount).GreaterThanOrEqualTo(0).WithErrorCode(CodigoNegativo)
                    .WithMessage(i => $"line {i.LineId}: negative net_amount is only allowed when status is REFUNDED");
            });
        });
    }
}
