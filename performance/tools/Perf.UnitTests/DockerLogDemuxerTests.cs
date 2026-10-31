using System.Text;
using PerfAgent.Docker;

namespace Perf.UnitTests;

public class DockerLogDemuxerTests
{
    private static byte[] Frame(byte stream, string text)
    {
        var payload = Encoding.UTF8.GetBytes(text);
        var header = new byte[] { stream, 0, 0, 0, 0, 0, 0, (byte)payload.Length };
        return header.Concat(payload).ToArray();
    }

    [Fact]
    public void JoinsStdoutAndStderrFrames()
    {
        var raw = Frame(1, "hello\n").Concat(Frame(2, "oops\n")).ToArray();

        Assert.Equal("hello\noops\n", DockerLogDemuxer.Demux(raw));
    }
}
