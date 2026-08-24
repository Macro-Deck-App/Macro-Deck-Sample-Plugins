using NUnit.Framework;

namespace MacroDeck.SampleVirtualProfilePlugin.Tests;

/// <summary>
/// The localization set is generated from <c>Localization/*.resx</c>, so these guard the wiring rather
/// than any wording: a missing catalog registration leaves every label showing its raw key, and a key
/// present in a translation but not in the default-language file can never resolve at all.
/// </summary>
[TestFixture]
public sealed class LocalizationTests
{
	[Test]
	public void The_catalog_is_scoped_to_the_plugin_id()
	{
		Assert.That(Strings.LocalizationCatalog.Scope, Is.EqualTo("plugin:app.macro-deck.sample-virtual-profile"));
	}

	[Test]
	public void English_is_the_default_culture()
	{
		Assert.That(Strings.LocalizationCatalog.DefaultCulture, Is.EqualTo("en"));
		Assert.That(Strings.LocalizationCatalog.Cultures, Does.Contain("en"));
	}

	[Test]
	public void The_action_strings_come_from_the_catalog()
	{
		Assert.That(Strings.LocalizationCatalog.KeysOf("en"), Does.Contain("Actions.SetScene.Name"));
	}

	[Test]
	public void Every_key_the_default_culture_declares_resolves_to_text()
	{
		foreach (var key in Strings.LocalizationCatalog.KeysOf("en"))
		{
			Assert.That(Strings.LocalizationCatalog.TryGetTemplate("en", key, out var text), Is.True);
			Assert.That(text, Is.Not.Empty);
		}
	}

	/// <summary>
	/// Every culture the plugin ships carries every key the default language declares. MDLOC001 catches
	/// the other direction at build time - a key only a translation has - but a translation that is
	/// simply behind is not a build error, and this is what makes it a visible one.
	/// </summary>
	[Test]
	public void Every_culture_carries_every_key_the_default_language_declares()
	{
		var catalog = Strings.LocalizationCatalog;
		var expected = catalog.KeysOf(catalog.DefaultCulture);

		foreach (var culture in catalog.Cultures)
		{
			Assert.That(catalog.KeysOf(culture), Is.EquivalentTo(expected), $"culture '{culture}'");
		}
	}
}
