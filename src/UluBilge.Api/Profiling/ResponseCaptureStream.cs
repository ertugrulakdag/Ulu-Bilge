namespace UluBilge.Api.Profiling
{
    // Hedef akış ve kayıt tamponunun ömrünü çağıran yönetir.
    internal sealed class ResponseCaptureStream(Stream destination, MemoryStream capture, int limit) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => destination.CanWrite;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => destination.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => destination.FlushAsync(cancellationToken);

        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            destination.Write(buffer);
            Capture(buffer);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await destination.WriteAsync(buffer, cancellationToken);
            Capture(buffer.Span);
        }

        private void Capture(ReadOnlySpan<byte> buffer)
        {
            var count = (int)Math.Min(buffer.Length, limit - capture.Length);
            if (count > 0)
                capture.Write(buffer[..count]);
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
