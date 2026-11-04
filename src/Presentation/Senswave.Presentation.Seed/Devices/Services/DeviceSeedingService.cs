using Refit;
using Senswave.Abstractions.Resulting;
using Senswave.Devices.Api.Dashboards.CreateDashboard;
using Senswave.Devices.Api.Dashboards.SetWidgetOnDashboard;
using Senswave.Devices.Api.Devices.CreateDevice;
using Senswave.Devices.Api.Devices.UpdateDevice;
using Senswave.Devices.Api.Operations.CreateOperation;
using Senswave.Devices.Api.Widgets.Features.CreateWidget;
using Senswave.Devices.Domain.Devices.Enums;
using Senswave.Devices.Domain.Devices.Extensions;
using Senswave.Devices.Domain.Operations.Enums;
using Senswave.Devices.Domain.Operations.Extensions;
using Senswave.Devices.Domain.Widgets.Enums;
using Senswave.Devices.Domain.Widgets.Extensions;
using Senswave.Devices.Domain.Widgets.Types.Button;
using Senswave.Devices.Domain.Widgets.Types.Color;
using Senswave.Devices.Domain.Widgets.Types.Radio;
using Senswave.Devices.Domain.Widgets.Types.Radio.Model;
using Senswave.Devices.Domain.Widgets.Types.Slider;
using Senswave.Presentation.Seed.Devices.Clients;
using Senswave.Presentation.Seed.Devices.Interfaces;
using Senswave.Presentation.Seed.Devices.Options;
using Senswave.Presentation.Seed.Devices.Types;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Senswave.Presentation.Seed.Devices.Services;

public class DeviceSeedingService(
    IDeviceClient deviceClient,
    IDashboardClient dashboardClient,
    IWidgetClient widgetClient,
    IOperationClient operationClient,
    ILogger<DeviceSeedingService> logger) : IDeviceSeedingService
{
    public async Task<Result> SeedEmptyDevice(string accessToken, Guid homeId)
    {
        try
        {
            var createDeviceRquest = new CreateDeviceRequest
            {
                HomeId = homeId,
                Name = "Empty Device",
                Icon = "infinite-outline",
            };

            var deviceRespnse = await deviceClient.CreateDevice(accessToken, createDeviceRquest);

            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Home: {homeId}] Error seeding Empty Device.", homeId);
            return Result.Failure();
        }
    }

    public async Task<Result<DeviceOperations>> SeedDetector(string accessToken, Guid homeId, string topic)
    {
        try
        {
            var createDeviceRquest = new CreateDeviceRequest
            {
                HomeId = homeId,
                Name = "Movement Detector",
                Icon = "radio-outline"
            };

            var deviceRespnse = await deviceClient.CreateDevice(accessToken, createDeviceRquest);

            var stateOperationRequest = new CreateOperationRequest
            {
                DeviceId = deviceRespnse.Id,
                Name = "Detector State",
                Type = OperationType.Boolean.FromOperationType(),
                Topic = topic,
                Configuration = new()
                {
                    ["jsonNames"]=new JsonArray("detected"),
                    ["isJson"]=true
                }
            };

            var detectorStateOperation = await operationClient.CreateOperation(accessToken, stateOperationRequest);

            var deviceOperations = new DeviceOperations { BooleanOperationId = detectorStateOperation.Id };

            var updateDeviceRequest = new UpdateDeviceRequest
            {
                Type = DeviceTileType.Switch.FromDeviceTileType(),
                OperationId = detectorStateOperation.Id
            };

            await deviceClient.PatchDevice(accessToken, deviceRespnse.Id, updateDeviceRequest);

            var detectionOperationRequest = new CreateOperationRequest
            {
                DeviceId = deviceRespnse.Id,
                Name = "Detector State",
                Type = OperationType.Boolean.FromOperationType(),
                Topic = topic,
                Configuration = new()
                {
                    ["jsonNames"]=new JsonArray("detected"),
                    ["isJson"]=true
                }
            };

            var detectionOperationResponse = await operationClient.CreateOperation(accessToken, stateOperationRequest);

            return Result<DeviceOperations>.Success(deviceOperations);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Home: {homeId}] Error seeding Detector.", homeId);
            return Result<DeviceOperations>.Failure();
        }
    }

    #region Pico Controller

    public async Task<Result<DeviceOperations>> SeedPicoController(string accessToken, Guid homeId, Guid? roomId, PicoControllerOptions options)
    {
        var topic = options.GlobalTopic;

        try
        {
            var createDeviceRquest = new CreateDeviceRequest
            {
                HomeId = homeId,
                Name = "Pico Controller",
                Icon = "radio-outline"
            };

            if (roomId.HasValue)
            {
                createDeviceRquest.RoomId = roomId.Value;
            }

            var deviceRespnse = await deviceClient.CreateDevice(accessToken, createDeviceRquest);

            var dashboardRequest = new CreateDashboardRequest
            {
                DeviceId = deviceRespnse.Id,
                Name = "Modes",
                Icon = "radio-outline",
                Configuration = new()
                {
                    ["columns"] = 4,
                    ["rows"] = 6
                }
            };

            var dashboardResponse = await dashboardClient.CreateDashboard(accessToken, dashboardRequest);

            var modeOperation = await AddModeToController(accessToken, deviceRespnse.Id, dashboardResponse.Id, topic);

            var stateOperation = await AddStateToController(accessToken, deviceRespnse.Id, topic);

            var brightnessOperation = await AddBrightnessToController(accessToken, deviceRespnse.Id, dashboardResponse.Id, topic);

            var deviceOperations = new DeviceOperations
            {
                BooleanOperationId = stateOperation,
                NumberOperationId = brightnessOperation,
                OptionOperationId = modeOperation
            };

            await AddQuickBrightnessActions(accessToken, dashboardResponse.Id, brightnessOperation);

            var quickActionsDashboardRequest = new CreateDashboardRequest
            {
                DeviceId = deviceRespnse.Id,
                Name = "Quick Actions",
                Icon = "radio-outline",
                Configuration = new()
                {
                    ["columns"] = 4,
                    ["rows"] = 6
                }
            };

            var quickActionsDashboardResponse = await dashboardClient.CreateDashboard(accessToken, quickActionsDashboardRequest);

            await AddColorForColorMode(accessToken, deviceRespnse.Id, quickActionsDashboardResponse.Id, topic);

            await AddBrightnessDisplay(accessToken, quickActionsDashboardResponse.Id, brightnessOperation);

            await AddStateSwitch(accessToken, quickActionsDashboardResponse.Id, stateOperation);

            // Performance-test data: more operations (S2 lists them) and one large, full dashboard (S3 displays it).
            List<SeededOperation> operations =
            [
                new(stateOperation, OperationType.Boolean),
                new(brightnessOperation, OperationType.Number, Max: 1),
                new(modeOperation, OperationType.Options, Options: ControllerModeNames),
            ];

            for (var i = 1; i <= options.ExtraOperations; i++)
                operations.Add(await AddExtraOperation(accessToken, deviceRespnse.Id, topic, i));

            if (options.LargeDashboardRows > 0)
                await AddLargeDashboard(accessToken, deviceRespnse.Id, options.LargeDashboardRows, options.LargeDashboardColumns, operations);

            return Result<DeviceOperations>.Success(deviceOperations);
        }
        catch (ApiException ex)
        {
            var content = await ex.GetContentAsAsync<JsonObject>()!;

            logger.LogError(ex, "[Home: {homeId}] Error seeding Pico Controller. Content: {content}",
                homeId,
                content);

            return Result<DeviceOperations>.Failure();
        }
    }

    private async Task AddColorForColorMode(string accessToken, Guid deviceId, Guid dashboardId, string topic)
    {
        var hexColorOperationRequest = new CreateOperationRequest
        {
            DeviceId = deviceId,
            Name = "Color in Color Mode",
            Type = OperationType.HexColor.FromOperationType(),
            Topic = topic,
            Configuration = new()
            {
                ["jsonNames"] = new JsonArray("mode_data", "color"),
                ["isJson"] = true,
            }
        };

        var hexColorOperation = await operationClient.CreateOperation(accessToken, hexColorOperationRequest);

        var colorConfiguration = new ColorWidgetConfiguration();

        var colorWidgetData = JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(colorConfiguration))!;

        var createColorWidget = new CreateWidgetRequest
        {
            Name = "Color for color mode",
            Type = WidgetType.Color.FromWidgetType(),
            Configuration = colorWidgetData,
            OperationId = hexColorOperation.Id,
        };

        var colorWidget = await widgetClient.CreateWidget(accessToken, createColorWidget);

        var setColorWidgetOnDashboard = new SetWidgetOnDashboardRequest
        {
            Row = 0,
            Column = 0,
            RowSpan = 3,
            ColumnSpan = 4,
            WidgetId = colorWidget.Id
        };

        await dashboardClient.SetWidgetOnDashboard(accessToken, dashboardId, setColorWidgetOnDashboard);
    }

    private async Task AddBrightnessDisplay(string accessToken, Guid dashboard, Guid operation)
    {
        var displayWidgetRequest = new CreateWidgetRequest
        {
            Name = "Brightness Display",
            Type = WidgetType.Display.FromWidgetType(),
            Configuration = new JsonObject
            {
                ["unit"] = "%",
            },
            OperationId = operation
        };

        var displayResponse = await widgetClient.CreateWidget(accessToken, displayWidgetRequest);

        var setDisplayOnDashboardRequest = new SetWidgetOnDashboardRequest
        {
            Row = 4,
            Column = 0,
            RowSpan = 1,
            ColumnSpan = 4,
            WidgetId = displayResponse.Id
        };

        await dashboardClient.SetWidgetOnDashboard(accessToken, dashboard, setDisplayOnDashboardRequest);
    }

    private async Task AddStateSwitch(string accessToken, Guid dashboard, Guid operation)
    {
        var switchWidgetRequest = new CreateWidgetRequest
        {
            Name = "Controller State Switch",
            Type = WidgetType.Switch.FromWidgetType(),
            Configuration = [],
            OperationId = operation
        };

        var switchResponse = await widgetClient.CreateWidget(accessToken, switchWidgetRequest);

        var setSwitchOnDashboardRequest = new SetWidgetOnDashboardRequest
        {
            Row = 5,
            Column = 0,
            RowSpan = 1,
            ColumnSpan = 2,
            WidgetId = switchResponse.Id
        };

        await dashboardClient.SetWidgetOnDashboard(accessToken, dashboard, setSwitchOnDashboardRequest);
    }

    private async Task<Guid> AddModeToController(string accessToken, Guid deviceId, Guid dashboardId, string topic)
    {
        var modeOperationRequest = new CreateOperationRequest
        {
            DeviceId = deviceId,
            Name = "Controller Mode",
            Type = OperationType.Options.FromOperationType(),
            Topic = topic,
            Configuration = new()
            {
                ["jsonNames"] = new JsonArray("mode"),
                ["isJson"] = true,
                ["options"] = new JsonArray()
                {
                    new JsonObject
                    {
                        ["name"] = "white",
                        ["value"] = 0
                    },
                    new JsonObject
                    {
                        ["name"] = "color",
                        ["value"] = 1
                    },
                    new JsonObject
                    {
                        ["name"] = "rgb",
                        ["value"] = 2
                    },
                    new JsonObject
                    {
                        ["name"] = "loading",
                        ["value"] = 3
                    }
                }
            }
        };

        var modeOperation = await operationClient.CreateOperation(accessToken, modeOperationRequest);

        var radioButtonOptions = new RadioWidgetConfiguration
        {
            Options =
            [
                new RadioOption()
                {
                    DisplayName = "White",
                    OptionName = "white",
                    Icon = "bulb-outline",
                },
                new RadioOption()
                {
                    DisplayName = "Color",
                    OptionName = "color",
                    Icon = "color-palette-outline",
                },
                new RadioOption()
                {
                    DisplayName = "RGB",
                    OptionName = "rgb",
                    Icon = "color-filter-outline",
                },
                new RadioOption()
                {
                    DisplayName = "Loading",
                    OptionName = "loading",
                    Icon = "flash-outline",
                }
            ]
        };

        var radioButtonData = JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(radioButtonOptions))!;

        var createWidgetRequest = new CreateWidgetRequest
        {
            Name = "Radio Mode",
            Type = WidgetType.Radio.FromWidgetType(),
            Configuration = radioButtonData,
            OperationId = modeOperation.Id
        };

        var radioResponse = await widgetClient.CreateWidget(accessToken, createWidgetRequest);

        var setRadioOnDashboard = new SetWidgetOnDashboardRequest
        {
            Row = 5,
            Column = 0,
            RowSpan = 1,
            ColumnSpan = 4,
            WidgetId = radioResponse.Id
        };

        await dashboardClient.SetWidgetOnDashboard(accessToken, dashboardId, setRadioOnDashboard);

        return modeOperation.Id;
    }

    private async Task<Guid> AddStateToController(string accessToken, Guid deviceId, string topic)
    {
        var stateOperationRequest = new CreateOperationRequest
        {
            DeviceId = deviceId,
            Name = "Controller State",
            Type = OperationType.Boolean.FromOperationType(),
            Topic = topic,
            Configuration = new()
            {
                ["jsonNames"] = new JsonArray("working"),
                ["isJson"] = true
            }
        };

        var stateOperation = await operationClient.CreateOperation(accessToken, stateOperationRequest);

        var updateDeviceRequest = new UpdateDeviceRequest
        {
            Type = DeviceTileType.Switch.FromDeviceTileType(),
            OperationId = stateOperation.Id,
        };

        await deviceClient.PatchDevice(accessToken, deviceId, updateDeviceRequest);

        return stateOperation.Id;
    }

    private async Task<Guid> AddBrightnessToController(string accessToken, Guid deviceId, Guid dashboardId, string topic)
    {
        var brightnessOperationRequest = new CreateOperationRequest
        {
            DeviceId = deviceId,
            Name = "Controller Brightness",
            Type = OperationType.Number.FromOperationType(),
            Topic = topic,
            Configuration = new()
            {
                ["jsonNames"] = new JsonArray("brightness"),
                ["isJson"] = true,
                ["min"] = 0,
                ["max"] = 1
            }
        };

        var brightnessOperation = await operationClient.CreateOperation(accessToken, brightnessOperationRequest);

        var sliderOptions = new SliderWidgetConfiguration
        {
            Step = 0.01,
        };

        var sliderData = JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(sliderOptions))!;

        var createSliderWidgetRequest = new CreateWidgetRequest
        {
            Name = "Controller Brightness Slider",
            Type = WidgetType.Slider.FromWidgetType(),
            Configuration = sliderData,
            OperationId = brightnessOperation.Id,
        };

        var widgetId = await widgetClient.CreateWidget(accessToken, createSliderWidgetRequest);

        var setBrightnessOnDashboard = new SetWidgetOnDashboardRequest
        {
            Row = 4,
            Column = 0,
            RowSpan = 1,
            ColumnSpan = 4,
            WidgetId = widgetId.Id
        };

        await dashboardClient.SetWidgetOnDashboard(accessToken, dashboardId, setBrightnessOnDashboard);

        return brightnessOperation.Id;
    }

    private async Task AddQuickBrightnessActions(string accessToken, Guid dashboardId, Guid operationId)
    {
        await AddButton(accessToken, dashboardId, operationId, 1, 0, 0, 1, 2, "Full");

        await AddButton(accessToken, dashboardId, operationId, 0.40, 0, 2, 1, 2, "Medium");

        await AddButton(accessToken, dashboardId, operationId, 0.15, 1, 0, 1, 2, "Low");

        await AddButton(accessToken, dashboardId, operationId, 0.05, 1, 2, 1, 2, "Min");
    }

    private async Task AddButton(
        string accessToken,
        Guid dashboardId,
        Guid operationId,
        double value,
        int row,
        int col,
        int rowSpan,
        int colSpan,
        string name)
    {
        var buttonOptions = new ButtonWidgetConfiguration
        {
            Value = JsonValue.Create(value)
        };

        var buttonMinData = JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(buttonOptions))!;

        var createDeadButtonWidgetRequest = new CreateWidgetRequest
        {
            Name = name,
            Type = WidgetType.Button.FromWidgetType(),
            Configuration = buttonMinData,
            OperationId = operationId
        };

        var deadButtonResponse = await widgetClient.CreateWidget(accessToken, createDeadButtonWidgetRequest);

        var setDeadButtonWidgetOnDashboard = new SetWidgetOnDashboardRequest
        {
            Row = row,
            Column = col,
            RowSpan = rowSpan,
            ColumnSpan = colSpan,
            WidgetId = deadButtonResponse.Id
        };

        await dashboardClient.SetWidgetOnDashboard(accessToken, dashboardId, setDeadButtonWidgetOnDashboard);
    }

    #endregion

    #region Performance test data

    private const string LargeDashboardName = "Large";

    private static readonly string[] ExtraOptionNames = ["off", "low", "medium", "high"];

    private static readonly string[] ControllerModeNames = ["white", "color", "rgb", "loading"];

    private const double ExtraNumberMax = 100;

    /// <param name="Max">Upper bound of a number operation (lower bound 0): sets the slider step and the button value.</param>
    /// <param name="Options">Option names of an options operation: a radio widget must offer exactly values the operation accepts.</param>
    private sealed record SeededOperation(Guid Id, OperationType Type, double Max = 0, string[]? Options = null);

    /// <summary>
    /// One extra controller channel; the type rotates number, boolean, options, hex color. All channels share their own
    /// topic (&lt;topic&gt;/channels): a message on a topic is processed by every operation of that topic, so the extra
    /// channels must not join the controller topic that the S5 / S6 messages travel on.
    /// </summary>
    private async Task<SeededOperation> AddExtraOperation(string accessToken, Guid deviceId, string topic, int index)
    {
        var type = (index % 4) switch
        {
            1 => OperationType.Number,
            2 => OperationType.Boolean,
            3 => OperationType.Options,
            _ => OperationType.HexColor,
        };

        var configuration = type switch
        {
            OperationType.Number => new JsonObject
            {
                ["jsonNames"] = new JsonArray($"channel_{index}", "level"),
                ["isJson"] = true,
                ["min"] = 0,
                ["max"] = ExtraNumberMax
            },
            OperationType.Boolean => new JsonObject
            {
                ["jsonNames"] = new JsonArray($"channel_{index}", "power"),
                ["isJson"] = true
            },
            OperationType.Options => new JsonObject
            {
                ["jsonNames"] = new JsonArray($"channel_{index}", "preset"),
                ["isJson"] = true,
                ["options"] = new JsonArray(ExtraOptionNames
                    .Select((name, value) => (JsonNode)new JsonObject { ["name"] = name, ["value"] = value })
                    .ToArray())
            },
            _ => new JsonObject
            {
                ["jsonNames"] = new JsonArray($"channel_{index}", "color"),
                ["isJson"] = true
            },
        };

        var request = new CreateOperationRequest
        {
            DeviceId = deviceId,
            Name = $"Channel {index} {type}",
            Type = type.FromOperationType(),
            Topic = $"{topic}/channels",
            Configuration = configuration
        };

        var response = await operationClient.CreateOperation(accessToken, request);

        return type switch
        {
            OperationType.Number => new SeededOperation(response.Id, type, Max: ExtraNumberMax),
            OperationType.Options => new SeededOperation(response.Id, type, Options: ExtraOptionNames),
            _ => new SeededOperation(response.Id, type),
        };
    }

    /// <summary>
    /// A rows x columns dashboard with one 1x1 widget in every cell. Operations are reused round-robin, and number
    /// operations alternate between slider, display and button, so the dashboard mixes every widget type.
    /// </summary>
    private async Task AddLargeDashboard(string accessToken, Guid deviceId, int rows, int columns, List<SeededOperation> operations)
    {
        var dashboardRequest = new CreateDashboardRequest
        {
            DeviceId = deviceId,
            Name = LargeDashboardName,
            Icon = "grid-outline",
            Configuration = new()
            {
                ["columns"] = columns,
                ["rows"] = rows
            }
        };

        var dashboard = await dashboardClient.CreateDashboard(accessToken, dashboardRequest);

        var numberWidgets = 0;
        for (var cell = 0; cell < rows * columns; cell++)
        {
            var operation = operations[cell % operations.Count];

            var (widgetType, configuration) = operation.Type switch
            {
                OperationType.Boolean => (WidgetType.Switch, new JsonObject()),
                OperationType.Options => (WidgetType.Radio, RadioConfiguration(operation.Options!)),
                OperationType.HexColor => (WidgetType.Color, ToJson(new ColorWidgetConfiguration())),
                _ => (numberWidgets++ % 3) switch
                {
                    0 => (WidgetType.Slider, ToJson(new SliderWidgetConfiguration { Step = operation.Max / 100 })),
                    1 => (WidgetType.Display, new JsonObject { ["unit"] = "%" }),
                    _ => (WidgetType.Button, ToJson(new ButtonWidgetConfiguration { Value = JsonValue.Create(operation.Max / 2) })),
                },
            };

            var widget = await widgetClient.CreateWidget(accessToken, new CreateWidgetRequest
            {
                Name = $"Cell {cell + 1} {widgetType}",
                Type = widgetType.FromWidgetType(),
                Configuration = configuration,
                OperationId = operation.Id
            });

            await dashboardClient.SetWidgetOnDashboard(accessToken, dashboard.Id, new SetWidgetOnDashboardRequest
            {
                Row = cell / columns,
                Column = cell % columns,
                RowSpan = 1,
                ColumnSpan = 1,
                WidgetId = widget.Id
            });
        }
    }

    /// <summary>One radio option per option of the operation (names are short enough for the 12-character display name).</summary>
    private static JsonObject RadioConfiguration(string[] optionNames) => ToJson(new RadioWidgetConfiguration
    {
        Options = optionNames
            .Select(name => new RadioOption { DisplayName = name, OptionName = name, Icon = "ellipse-outline" })
            .ToList()
    });

    private static JsonObject ToJson<T>(T value) => JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(value))!;

    #endregion

    public async Task<Result> SeedPlantMonitor(string accessToken, Guid homeId)
    {
        try
        {
            var createDeviceRquest = new CreateDeviceRequest
            {
                HomeId = homeId,
                Name = "Plant Monitor",
                Icon = "leaf-outline"
            };

            var deviceRespnse = await deviceClient.CreateDevice(accessToken, createDeviceRquest);

            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Home: {homeId}] Error seeding Plant Monitor.", homeId);
            return Result.Failure();
        }
    }
}
