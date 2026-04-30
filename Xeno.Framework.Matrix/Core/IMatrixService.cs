using System;
using System.Threading.Tasks;

namespace Xeno.Framework.Matrix.Core
{
    /// <summary>
    /// Abstraction for a matrix router control service (PN-8080, Blackmagic Videohub, ...).
    /// Ports use 1-based indexing across the public API regardless of the wire protocol.
    /// </summary>
    public interface IMatrixService : IDisposable
    {
        string Host { get; set; }
        int Port { get; set; }
        ConnectionMode Mode { get; set; }
        bool AutoReconnect { get; set; }

        /// <summary>
        /// Host-supplied maximum input count. When set before <see cref="ConnectAsync"/>,
        /// the initial <see cref="DeviceInfo.InputCount"/> is seeded with this value so the UI
        /// can pre-build the switching grid at the target size without a post-dump rebuild.
        /// Ignored by fixed-size devices (e.g. PN-8080 is always 8×8).
        /// </summary>
        int MaxInputs { get; set; }

        /// <summary>Counterpart for outputs. See <see cref="MaxInputs"/>.</summary>
        int MaxOutputs { get; set; }

        DeviceInfo Device { get; }
        bool IsConnected { get; }
        bool IsReconnecting { get; }

        event EventHandler<bool> ConnectionStateChanged;
        event EventHandler<LogEntry> Logged;
        event EventHandler DeviceInfoChanged;
        event EventHandler StateUpdated;

        Task ConnectAsync();
        Task DisconnectAsync();

        /// <summary>Route <paramref name="input"/> to <paramref name="output"/> (1-based).</summary>
        Task RouteAsync(int input, int output);

        /// <summary>Route the given input to every output.</summary>
        Task RouteAllAsync(int input);

        /// <summary>Point-to-point: input N → output N for N in 1..OutputCount.</summary>
        Task PtpAsync();

        /// <summary>Re-read the full routing state from the device.</summary>
        Task RefreshAsync();

        /// <summary>
        /// Apply multiple routing changes as a single logical transaction.
        /// Videohub batches them into one VIDEO OUTPUT ROUTING block (atomic at protocol level).
        /// PN-8080 falls back to sequential RouteAsync calls (no atomic protocol support).
        /// </summary>
        /// <param name="outputToInput">Map of output port (1-based, key) to input port (1-based, value). Empty map returns immediately.</param>
        Task RouteBatchAsync(System.Collections.Generic.IDictionary<int, int> outputToInput);

        /// <summary>Return the current cached input (1-based) for <paramref name="output"/>, or 0 if unknown.</summary>
        int GetInputFor(int output);
    }
}
