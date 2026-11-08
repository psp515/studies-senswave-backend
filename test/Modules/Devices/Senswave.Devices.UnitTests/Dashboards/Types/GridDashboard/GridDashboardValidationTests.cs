using Senswave.Devices.Domain.Dashboards.Enums;
using System.Text.Json.Nodes;

namespace Senswave.Devices.UnitTests.Dashboards.Types.GridDashboard;

[Trait("Collection", "UnitTests")]
public class GridDashboardValidationTests : BaseDashboardTest
{
    [Fact]
    public async Task DashboardInitialized()
    {
        // Arrange
        var configuration = new JsonObject
        {
            ["rows"] = 3,
            ["columns"] = 3,
        };

        // Act
        var result = await dashboardFactory.Initialize(Guid.NewGuid(), "name", "icon", DashboardType.Grid, configuration);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(6, 6)]
    [InlineData(11, 4)]
    [InlineData(0, 0)]
    public async Task FailedToInitalizeDashboard(int rows, int columns)
    {
        // Arrange
        var configuration = new JsonObject
        {
            ["rows"] = rows,
            ["columns"] = columns,
        };

        // Act
        var result = await dashboardFactory.Initialize(Guid.NewGuid(), "name", "icon", DashboardType.Grid, configuration);

        // Assert
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task FailedToInitalizeWhenPositionedWidgetProvided()
    {
        // Arrange
        var configuration = new JsonObject
        {
            ["rows"] = 3,
            ["columns"] = 3,
            ["positionedWidgets"] = new JsonArray(new JsonObject
            {
                ["widgetId"] = Guid.NewGuid(),
                ["row"] = 0,
                ["rowSpan"] = 2,
                ["column"] = 0,
                ["columnSpan"] = 2,
            })
        };

        // Act
        var result = await dashboardFactory.Initialize(Guid.NewGuid(), "name", "icon", DashboardType.Grid, configuration);

        // Assert
        Assert.True(result.IsFailure);
    }

    [Theory]
    [InlineData(10, 4)]
    [InlineData(10, 5)]
    public async Task DashboardWithAWidgetInEveryCellIsValid(int rows, int columns)
    {
        // Arrange
        var widgets = new JsonArray();
        for (var cell = 0; cell < rows * columns; cell++)
        {
            widgets.Add(new JsonObject
            {
                ["widgetId"] = Guid.NewGuid(),
                ["row"] = cell / columns,
                ["rowSpan"] = 1,
                ["column"] = cell % columns,
                ["columnSpan"] = 1,
            });
        }

        var dashboard = GridDashboard;
        dashboard.Id = Guid.NewGuid();
        dashboard.Configuration = new JsonObject
        {
            ["rows"] = rows,
            ["columns"] = columns,
            ["positionedWidgets"] = widgets,
        };

        dashboardQueryRepository.Setup(x => x.GetDashboard(dashboard.Id, default))
            .ReturnsAsync(dashboard);

        var created = await dashboardFactory.Create(dashboard.Id, default);

        // Act
        var result = await created.Data.Validate();

        // Assert
        Assert.True(result.IsSuccess);
    }
}
