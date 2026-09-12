using MacroDeck.Plugin.Hosting.Integrations;
using MacroDeck.Plugin.Hosting.Integrations.HostApis;
using MacroDeck.Plugin.Protocol.Handshake;
using MacroDeck.SampleWeatherPlugin.Actions;
using MacroDeck.SampleWeatherPlugin.ConfigFlow;
using MacroDeck.SampleWeatherPlugin.Weather;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.ConfigFlow;
using MacroDeck.Sdk.Events;
using MacroDeck.Sdk.Variables;
using MacroDeck.Sdk.Weather;
using Serilog;

namespace MacroDeck.SampleWeatherPlugin;

/// <summary>
/// One integration wiring actions, variables, an event, a config flow and a weather provider to the
/// same synthetic reading, so the pieces demonstrate how they fit together rather than standing alone.
/// </summary>
public sealed class WeatherIntegration : IPluginIntegration, IVariableProvider, IEventProvider,
	IConfigFlowProvider, IWeatherProvider
{
	internal const string StationId = "primary";

	internal const string WeatherRefreshedEventId = "weather-refreshed";

	/// <summary>The conditions the "force weather condition" action offers, curated down from every
	/// <see cref="WeatherCondition"/> value so the picker stays short.</summary>
	internal static readonly IReadOnlyList<WeatherCondition> SelectableConditions =
	[
		WeatherCondition.Clear, WeatherCondition.PartlyCloudy, WeatherCondition.Overcast,
		WeatherCondition.Rain, WeatherCondition.Thunderstorm, WeatherCondition.Snow
	];

	private readonly IPluginCatalogNotifier _catalogNotifier;
	private readonly ILogger _logger;

	private IIntegrationContext? _context;

	// Constructor injection: the integration is registered through RegisterIntegration<T>() (Program.cs)
	// and built by DI, so anything the container knows can be taken here.
	public WeatherIntegration(IPluginCatalogNotifier catalogNotifier, ILogger logger)
	{
		_catalogNotifier = catalogNotifier;
		_logger = logger.ForContext<WeatherIntegration>();
		Station = new SyntheticWeatherStation(this);
		Actions = [new RefreshWeatherAction(this), new SetAlertThresholdAction(this), new SetConditionAction(this)];
	}

	public IReadOnlyList<IActionDefinition> Actions { get; }

	/// <summary>The configured location, reported by both the weather snapshot and the location variable.</summary>
	internal string LocationName { get; private set; } = "Berlin, Germany";

	/// <summary>The temperature above which a refresh reports an alert, set by the slider action.</summary>
	internal double AlertThresholdCelsius { get; set; } = 30;

	internal SyntheticWeatherStation Station { get; }

	public async Task InitializeAsync(IIntegrationContext context)
	{
		_context = context;

		// Where a plugin's config-flow story completes: the flow persisted the location, this reads it back.
		var entries = await context.Config.GetEntriesAsync();
		if (entries.Count > 0)
		{
			var stored = await context.Config.GetStringAsync(entries[0].Id, LocationConfigFlow.LocationFieldName);
			if (!string.IsNullOrWhiteSpace(stored))
			{
				LocationName = stored;
			}
		}

		Station.Tick();

		_logger.Information("Initialized with location {Location} and alert threshold {ThresholdCelsius}°C.",
			LocationName,
			AlertThresholdCelsius);

		// The host describes capabilities concurrently with this method, so a first describe can capture
		// the default location before the config read above finished. Telling the host both catalogues
		// are stale is what makes the widget, the variable and the config card agree.
		_catalogNotifier.CatalogChanged(CapabilityKinds.Weather, reason: "location config applied");
		_catalogNotifier.CatalogChanged(CapabilityKinds.Variables, reason: "location config applied");
	}

	public Task ShutdownAsync()
	{
		_context = null;
		return Task.CompletedTask;
	}

	public IReadOnlyList<VariableDefinition> Variables { get; } =
	[
		VariableDefinition.Eager("sample_location", VariableType.Text) with { Id = "location" },
		VariableDefinition.Eager("sample_temperature_celsius", VariableType.Numeric, decimalPlaces: 1)
			with { Id = "temperature-celsius", Unit = "°C" },
		// Writable, so a Slider widget bound to it sets the threshold and reads the real one back.
		VariableDefinition.Eager("sample_alert_threshold_celsius", VariableType.Numeric)
			with { Id = "alert-threshold-celsius", Unit = "°C", Write = new VariableWriteCapability() }
	];

	public ValueTask<VariableReading> ReadAsync(string localId, CancellationToken cancellationToken = default)
		=> ValueTask.FromResult(localId switch
		{
			"location" => VariableReading.Of(LocationName),
			"temperature-celsius" => VariableReading.Of(Station.LastSnapshot.Temperature),
			"alert-threshold-celsius" => VariableReading.Of(AlertThresholdCelsius, SetAlertThresholdAction.Min,
				SetAlertThresholdAction.Max, 1),
			_ => VariableReading.Unavailable
		});

	/// <summary>Only called for the threshold, the one variable declaring a write.</summary>
	public ValueTask<VariableWriteResult> SetValueAsync(string localId, object? value, CancellationToken cancellationToken = default)
	{
		if (value is not (double or int or long))
		{
			return ValueTask.FromResult(VariableWriteResult.InvalidValue());
		}

		AlertThresholdCelsius = Math.Clamp(Convert.ToDouble(value), SetAlertThresholdAction.Min, SetAlertThresholdAction.Max);
		return ValueTask.FromResult(VariableWriteResult.Applied());
	}

	// ProviderName is deliberately not implemented: the host falls back to the manifest name, so the one
	// place this plugin states its name stays manifest.json.
	public IReadOnlyList<EventDefinition> EventDefinitions { get; } =
	[
		new EventDefinition
		{
			Id = WeatherRefreshedEventId,
			Name = Strings.Events.WeatherRefreshed.Name(),
			Description = Strings.Events.WeatherRefreshed.Description(),
			PayloadParameters =
			[
				ActionParameter.Number("temperatureCelsius", Strings.Events.WeatherRefreshed.Temperature.Label()),
				ActionParameter.Text("condition", Strings.Events.WeatherRefreshed.Condition.Label()),
				ActionParameter.Toggle("isAlert", Strings.Events.WeatherRefreshed.IsAlert.Label())
			]
		}
	];

	/// <summary>Publishing is fire-and-forget by contract, so this never throws. The context is only null
	/// before <see cref="InitializeAsync"/> or after <see cref="ShutdownAsync"/>.</summary>
	internal void PublishWeatherRefreshed(WeatherSnapshot snapshot, bool isAlert)
		=> _context?.Events.Publish(WeatherRefreshedEventId, new Dictionary<string, object?>
		{
			["temperatureCelsius"] = snapshot.Temperature,
			["condition"] = snapshot.Condition.ToString(),
			["isAlert"] = isAlert
		});

	public IConfigFlow CreateConfigFlow() => new LocationConfigFlow();

	public bool AllowsMultipleConfigurations => false;

	public IReadOnlyList<WeatherStationInstance> GetInstances() => [new WeatherStationInstance(StationId, LocationName)];

	public IWeatherStation? GetStation(string instanceId)
		=> string.Equals(instanceId, StationId, StringComparison.Ordinal) ? Station : null;
}
