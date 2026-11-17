using System.Buffers.Binary;
using System.Text;

namespace PerfAgent.Docker;

/// <summary>Docker multiplexes stdout/stderr of non-TTY containers in 8-byte-header frames.</summary>
public static class DockerLogDemuxer
{
    public static string Demux(byte[] raw)
    {
        var text = new StringBuilder();
        var offset = 0;

        while (offset + 8 <= raw.Length)
        {
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(raw.AsSpan(offset + 4, 4));
            var end = Math.Min(offset + 8 + length, raw.Length);
            text.Append(Encoding.UTF8.GetString(raw, offset + 8, end - offset - 8));
            offset = end;
        }

        return text.ToString();
    }
}
