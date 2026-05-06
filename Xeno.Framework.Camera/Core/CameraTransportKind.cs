namespace Xeno.Framework.Camera.Core
{
    /// <summary>
    /// Wire-level transport options for a camera service.
    /// Serial variants share the same byte payload; IP variants wrap the payload in transport-specific framing.
    /// </summary>
    public enum CameraTransportKind
    {
        Rs232 = 0,
        Rs422 = 1,
        Rs485 = 2,
        UdpVisca = 10,   // Sony VISCA over IP UDP (default port 52381)
        TcpVisca = 11    // reserved for v2
    }
}
