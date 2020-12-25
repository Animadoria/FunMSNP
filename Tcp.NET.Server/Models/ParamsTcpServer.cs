namespace Tcp.NET.Server.Models
{
    public struct ParamsTcpServer : IParamsTcpServer
    {
        public int Port { get; set; }
        public string EndOfLineCharacters { get; set; }
        public string ConnectionSuccessString { get; set; }
    }
}
