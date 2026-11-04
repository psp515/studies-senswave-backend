using FluentValidation;
using Senswave.Devices.Domain.Dashboards.Enums;

namespace Senswave.Devices.Domain.Dashboards.Types.Gird;

public sealed class GridDashboardValidator : AbstractValidator<GridDashboard>
{
    public GridDashboardValidator()
    {
        RuleFor(x => x.DeviceId)
            .NotEmpty();

        RuleFor(x => x.Icon)
            .MaximumLength(AllowedLengths.Icons.MaxLength);

        RuleFor(x => x.Name)
            .Length(AllowedLengths.Names.MinLength, AllowedLengths.Names.MaxLength);

        RuleFor(x => x.Type)
            .NotEqual(DashboardType.Empty)
            .NotEqual(DashboardType.Invalid);

        RuleFor(x => x.Configuration.Rows)
            .InclusiveBetween(1, 10);

        RuleFor(x => x.Configuration.Columns)
            .InclusiveBetween(1, 5);

        RuleFor(x => x.Configuration.PositionedWidgets)
            .Must((dashboard, widgets) => widgets.Count <= dashboard.Configuration.Rows * dashboard.Configuration.Columns)
            .WithMessage("A dashboard cannot hold more widgets than it has cells.");
    }
}
