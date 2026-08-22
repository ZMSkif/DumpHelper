using System.Windows.Media.Imaging;
using FluentAssertions;
using Xunit;

namespace DupFinder.Imaging.Tests;

/// <summary>
/// Проект DupFinder.Imaging наполняется на этапе 4 (EXIF, dHash, превью).
/// Пока проверяем главное допущение, на котором он построен: встроенный
/// в Windows декодер (WIC) доступен и читает картинку из потока (ТЗ, приложение Б).
/// </summary>
public class WicAvailabilityTests
{
    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    [Fact]
    public void Декодер_читает_размеры_картинки_из_потока()
    {
        using var stream = File.OpenRead(Fixture("gradient-4x2.png"));

        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.IgnoreColorProfile,
            BitmapCacheOption.None);
        var frame = decoder.Frames[0];

        frame.PixelWidth.Should().Be(4);
        frame.PixelHeight.Should().Be(2);
    }

    [Fact]
    public void Уменьшённое_декодирование_и_Freeze_работают()
    {
        using var stream = File.OpenRead(Fixture("gradient-4x2.png"));

        var image = new BitmapImage();
        image.BeginInit();
        image.StreamSource = stream;
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        image.DecodePixelWidth = 9;
        image.DecodePixelHeight = 8;
        image.EndInit();
        image.Freeze();

        image.IsFrozen.Should().BeTrue("объекты WIC передаются в UI только замороженными");
        image.PixelWidth.Should().Be(9);
        image.PixelHeight.Should().Be(8);
    }

    [Fact]
    public void Серый_формат_отдаёт_пиксели_построчно()
    {
        using var stream = File.OpenRead(Fixture("gradient-4x2.png"));
        var source = BitmapFrame.Create(stream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);

        var gray = new FormatConvertedBitmap(source, System.Windows.Media.PixelFormats.Gray8, null, 0);
        var pixels = new byte[gray.PixelWidth * gray.PixelHeight];
        gray.CopyPixels(pixels, gray.PixelWidth, 0);

        pixels.Should().HaveCount(8);
        pixels[0].Should().BeLessThan(pixels[3], "первая строка — градиент от чёрного к белому");
    }
}
