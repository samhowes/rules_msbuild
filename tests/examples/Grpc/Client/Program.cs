using System;
using System.Threading.Tasks;
using Grpc;
using Grpc.Net.Client;

namespace Client
{
    class Program
    {
        static async Task Main(string[] args)
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            var channel = GrpcChannel.ForAddress("http://localhost:5000");
            var client = new Greeter.GreeterClient(channel);
            var reply = await client.SayHelloAsync(new HelloRequest() {Name = "Dotnet Bazel"});
            Console.WriteLine(reply.Message);
        }
    }
}
