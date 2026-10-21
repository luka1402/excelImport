using ExcelImporter.Application.Files;

namespace ExcelImporter.Infrastructure.Excel;

/// <summary>Zajednički limit stvarno raspakovanih bajtova za sve delove jedne radne sveske; deklarisane veličine u ZIP-u nisu pouzdane.</summary>
internal sealed class ByteBudget(long limit)
{
    private long remaining = limit;

    public Stream Wrap(Stream inner) => new BudgetStream(inner, this);

    private void Consume(int count)
    {
        remaining -= count;
        if (remaining < 0)
        {
            throw new UnreadableExcelFileException("Fajl je prevelik nakon raspakivanja.");
        }
    }

    private sealed class BudgetStream(Stream inner, ByteBudget budget) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = inner.Read(buffer, offset, count);
            budget.Consume(read);
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            var read = inner.Read(buffer);
            budget.Consume(read);
            return read;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
