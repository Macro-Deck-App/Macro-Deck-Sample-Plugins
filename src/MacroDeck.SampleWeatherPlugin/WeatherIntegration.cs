using MacroDeck.Plugin.Hosting.Integrations;
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

	public IReadOnlyList<ProvidedVariable> ProvidedVariables { get; } =
	[
		new ProvidedVariable("sample_location", VariableType.Text) { DefinitionId = "location" },
		new ProvidedVariable("sample_temperature_celsius", VariableType.Numeric, DecimalPlaces: 1)
		{
			DefinitionId = "temperature-celsius"
		}
	];

	public Task<object?> GetValueAsync(string name, CancellationToken cancellationToken)
		=> Task.FromResult(name switch
		{
			"sample_location" => (object?)LocationName,
			"sample_temperature_celsius" => Station.LastSnapshot.Temperature,
			_ => null
		});

	// ProviderName is deliberately not implemented: the host falls back to the manifest name, so the one
	// place this plugin states its name stays manifest.json.
	public IReadOnlyList<EventDefinition> EventDefinitions { get; } =
	[
		new EventDefinition
		{
			Id = WeatherRefreshedEventId,
			Name = "Weather refreshed",
			Description = "Raised whenever the sample's synthetic weather reading changes.",
			PayloadParameters =
			[
				ActionParameter.Number("temperatureCelsius", "Temperature (°C)"),
				ActionParameter.Text("condition", "Condition"),
				ActionParameter.Toggle("isAlert", "Above alert threshold")
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
