using Progressing.Core;

namespace Progressing.Core.Tests;

/// <summary>时间 ↔ 像素映射：全长恒等于 24h；镜像 = 系数 1-n。</summary>
public class TimeMapperTests
{
    private static TimeSpan T(int minutes) => TimeSpan.FromMinutes(minutes);

    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(720, 0.5)]   // 12:00
    [InlineData(1440, 1.0)]  // 24:00
    public void Normalize_MapsDayLinear(int minutes, double expected)
        => Assert.Equal(expected, TimeMapper.Normalize(T(minutes)), 6);

    [Theory]
    [InlineData(-60, 0.0)]   // 超界收敛
    [InlineData(1500, 1.0)]  // 25:00 → 1.0
    public void Normalize_ClampsOutOfRange(int minutes, double expected)
        => Assert.Equal(expected, TimeMapper.Normalize(T(minutes)), 6);

    [Theory]
    [InlineData(false, 0, 0.0)]
    [InlineData(false, 360, 0.25)]  // 06:00
    [InlineData(true, 0, 1.0)]      // 镜像：0:00 → 最右
    [InlineData(true, 360, 0.75)]   // 镜像：06:00 → 0.75
    public void NormalizeMirrored_ReflectsWhenEnabled(bool mirrored, int minutes, double expected)
        => Assert.Equal(expected, TimeMapper.NormalizeMirrored(T(minutes), mirrored), 6);

    [Theory]
    [InlineData(360, 600, false, 150.0)]   // 06:00 在 600px 上 → 150
    [InlineData(1080, 600, false, 450.0)]  // 18:00 → 450
    [InlineData(360, 600, true, 450.0)]    // 镜像 06:00 → 450
    public void Map_LinearOverLength(int minutes, double length, bool mirrored, double expected)
        => Assert.Equal(expected, TimeMapper.Map(T(minutes), length, mirrored), 6);

    [Theory]
    [InlineData(150.0, 600, false, 360)]   // 150px → 06:00
    [InlineData(450.0, 600, true, 360)]    // 镜像 450px → 06:00
    [InlineData(600.0, 600, false, 1440)]  // 末端 → 24:00
    public void Unmap_InverseOfMap(double pos, double length, bool mirrored, int expectedMinutes)
        => Assert.Equal(expectedMinutes, (int)Math.Round(TimeMapper.Unmap(pos, length, mirrored).TotalMinutes));
}
