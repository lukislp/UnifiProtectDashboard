using UnifiCameraDashboard.Services;

namespace UnifiCameraDashboard.Tests.Services;

public class UrlCredentialsTests
{
    [Theory]
    [InlineData("rtsp://admin:secret@192.168.1.10:7447/cam-1?channel=0", "rtsp://192.168.1.10:7447/cam-1?channel=0")]
    [InlineData("rtsps://admin:secret@nvr.local:7441/abc", "rtsps://nvr.local:7441/abc")]
    [InlineData("rtsp://admin@nvr.local/abc", "rtsp://nvr.local/abc")]
    [InlineData("https://user:pw@host/path", "https://host/path")]
    public void Strip_RemovesUserInfo(string input, string expected)
    {
        Assert.Equal(expected, UrlCredentials.Strip(input));
    }

    [Fact]
    public void Strip_RemovesPasswordContainingAtSign()
    {
        var stripped = UrlCredentials.Strip("rtsp://admin:p@ss@w0rd@nvr.local:7447/cam");

        Assert.Equal("rtsp://nvr.local:7447/cam", stripped);
    }

    [Theory]
    [InlineData("rtsp://nvr.local:7447/cam-1?channel=0")]
    [InlineData("rtsp://nvr.local/cam?token=a@b")]
    [InlineData("not a url")]
    public void Strip_LeavesUrlsWithoutCredentialsUntouched(string input)
    {
        Assert.Equal(input, UrlCredentials.Strip(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Strip_ReturnsEmptyForNullOrEmpty(string? input)
    {
        Assert.Equal(string.Empty, UrlCredentials.Strip(input));
    }
}
