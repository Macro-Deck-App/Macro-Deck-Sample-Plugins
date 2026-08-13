using MacroDeck.Sdk.Weather;

namespace MacroDeck.SampleWeatherPlugin.Weather;

/// <summary>
/// The sample's single station. <see cref="Tick"/> computes a plausible reading and caches it;
/// <see cref="GetSnapshotAsync"/> hands the cached value back, which is the shape
/// <see cref="IWeatherStation"/> asks for - a widget reading the station never waits on I/O.
/// </summary>
internal sealed class SyntheticWeatherStation(WeatherIntegration integration) : IWeatherStation
{
	private int _tickCount;
	private WeatherCondition? _forcedCondition;

	/// <summary>The most recent reading, exposed synchronously so the temperature variable echoes exactly
	/// what <see cref="GetSnapshotAsync"/> serves.</summary>
	internal WeatherSnapshot LastSnapshot { get; private set; } = WeatherSnapshot.Unavailable();

	public Task<WeatherSnapshot> GetSnapshotAsync(CancellationToken ct) => Task.FromResult(LastSnapshot);

	/// <summary>Overrides the condition the next <see cref="Tick"/> reports.</summary>
	internal void SetForcedCondition(WeatherCondition condition) => _forcedCondition = condition;

	/// <summary>
	/// Advances the reading by one step, caches it and republishes the <c>weather-refreshed</c> event.
	/// A real provider would poll its API on its own interval and cache the result here instead.
	/// </summary>
	internal void Tick()
	{
		_tickCount++;

		// A sine wave rather than randomness, so a demo deck shows a believable rise and fall and the
		// sample's own tests get a deterministic reading.
		var temperature = Math.Round(15 + 6 * Math.Sin(_tickCount * 0.5), 1);
		var condition = _forcedCondition ?? (temperature switch
		{
			< 0 => WeatherCondition.Snow,
			< 12 => WeatherCondition.Overcast,
			< 20 => WeatherCondition.PartlyCloudy,
			_ => WeatherCondition.Clear
		});

		var today = DateOnly.FromDateTime(DateTime.UtcNow);
		LastSnapshot = new WeatherSnapshot
		{
			IsAvailable = true,
			LocationName = integration.LocationName,
			Temperature = temperature,
			ApparentTemperature = temperature - 1,
			Condition = condition,
			IsDay = DateTimeOffset.UtcNow.Hour is >= 6 and < 20,
			Unit = TemperatureUnit.Celsius,
			Days = [new WeatherForecastDay(today, condition, temperature - 4, temperature + 4)]
		};

		integration.PublishWeatherRefreshed(LastSnapshot, isAlert: temperature >= integration.AlertThresholdCelsius);
	}
}
