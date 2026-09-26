using System.Globalization;
using GitBinder.Desktop.Localization;

namespace GitBinder.Tests;

public sealed class LocalizationServiceTests
{
    [Theory]
    [InlineData("zh-CN", "en-US", "en-US", "Projects")]
    [InlineData("en-US", "zh-CN", "zh-CN", "项目")]
    [InlineData("en-US", "fr-FR", "zh-CN", "项目")]
    public async Task SetCulture_UpdatesStringsAndNotifiesIndexerBindings(
        string initialCulture, string requestedCulture, string expectedCulture, string expectedText)
    {
        var service = new LocalizationService();
        await service.SetCultureAsync(new CultureInfo(initialCulture));
        var previousText = service["Nav.Projects"];
        var propertyChanges = new List<(string? Property, string Culture, string Text)>();
        var cultureChanges = new List<(string Culture, string Text)>();
        service.PropertyChanged += (sender, args) =>
        {
            Assert.Same(service, sender);
            propertyChanges.Add((args.PropertyName, service.CurrentCulture.Name, service["Nav.Projects"]));
        };
        service.CultureChanged += (sender, _) =>
        {
            Assert.Same(service, sender);
            cultureChanges.Add((service.CurrentCulture.Name, service["Nav.Projects"]));
        };

        await service.SetCultureAsync(new CultureInfo(requestedCulture));

        Assert.Equal(expectedCulture, service.CurrentCulture.Name);
        Assert.Equal(expectedText, service["Nav.Projects"]);
        Assert.Equal(expectedText, service.GetString("Nav.Projects"));
        Assert.NotEqual(previousText, service["Nav.Projects"]);
        // Avalonia 通过实际 CLR 属性名解析索引变更，不能只发 WPF 风格的 Item[]。
        var propertyChange = Assert.Single(propertyChanges);
        Assert.Equal("Item", propertyChange.Property);
        var indexer = typeof(LocalizationService).GetProperty(propertyChange.Property!);
        Assert.NotNull(indexer);
        Assert.Single(indexer.GetIndexParameters());
        Assert.Equal(expectedCulture, propertyChange.Culture);
        Assert.Equal(expectedText, propertyChange.Text);
        Assert.Equal((expectedCulture, expectedText), Assert.Single(cultureChanges));
    }
}
