using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Hosting;
using System.Buffers;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ngdb
{
    public class NgDbServer : BackgroundService
    {
        private readonly IConnectionListenerFactory factory;

        public NgDbServer(IConnectionListenerFactory factory)
        {
            this.factory = factory;
        }

        protected async override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var listener = await factory.BindAsync(new IPEndPoint(IPAddress.Loopback, 2001));

            var connection = await listener.AcceptAsync();

            while (true)
            {
                var result = await connection.Transport.Input.ReadAsync();

                if (result.IsCompleted) break;

                var line = GetUtf8String(result.Buffer);

                await connection.Transport.Output.WriteAsync(Encoding.UTF8.GetBytes("OK"));

                connection.Transport.Input.AdvanceTo(result.Buffer.End);
            }
        }

        private string GetUtf8String(ReadOnlySequence<byte> buffer)
        {
            if (buffer.IsSingleSegment) return Encoding.UTF8.GetString(buffer.First.Span);

            return string.Create((int)buffer.Length, buffer, (span, sequence) =>
            {
                foreach (var segment in sequence)
                {
                    Encoding.UTF8.GetChars(segment.Span, span);

                    span = span.Slice(segment.Length);
                }
            });
        }
    }
}
