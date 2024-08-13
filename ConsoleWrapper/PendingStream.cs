using System.Collections.Concurrent;
using System.Data;
using System.Reflection;

namespace ConsoleWrapperLib;

internal class PendingStream : Stream
{
    public struct PendingChunk
    {
        public byte[] Data;
        public ConsoleColor Background;
        public ConsoleColor Foreground;
    }

    //private long lastRead = 0;
    private ConcurrentQueue<PendingChunk> pending = new();
    private MemoryStream buffer = new();

    public PendingStream()
    {

    }

    public PendingChunk? ReadPending()
    {
        if (pending.TryDequeue(out PendingChunk ret))
            return ret;
        return null;
        /*
        byte[] data = new byte[buffer.Length - lastRead];
        buffer.Position = lastRead;
        buffer.Read(data, 0, data.Length);
        lastRead = buffer.Position;
        return data;
        */
    }

    public void AddPending(PendingChunk chunk)
    {
        pending.Enqueue(chunk);
    }

    public override bool CanRead => buffer.CanRead;

    public override bool CanSeek => buffer.CanSeek;

    public override bool CanWrite => buffer.CanWrite;

    public override long Length
    {
        get
        {
            lock (buffer)
            {
                return buffer.Length;
            }
        }
    }

    public override long Position
    {
        get
        {
            lock (buffer)
            {
                return buffer.Position;
            }
        }
        set
        {
            lock (buffer)
            {
                buffer.Position = value;
            }
        }
    }

    public override void Flush()
    {
        lock (buffer)
        {
            buffer.Flush();
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        lock (this.buffer)
        {
            return this.buffer.Read(buffer, offset, count);
        }
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        lock (buffer)
        {
            return buffer.Seek(offset, origin);
        }
    }

    public override void SetLength(long value)
    {
        lock (buffer)
        {
            buffer.SetLength(value);
        }
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        lock (this.buffer)
        {
            this.buffer.Write(buffer, offset, count);
            byte[] specific = new byte[count];
            Array.Copy(buffer, offset, specific, 0, count);
            pending.Enqueue(new()
            {
                Data = specific,
                Background = Console.BackgroundColor,
                Foreground = Console.ForegroundColor,
            });
        }
    }
}
