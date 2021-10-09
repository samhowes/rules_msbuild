# //tests/examples/Grpc

Create this directory with:
```shell script
# define Server and Client stubs in a standalone class library
dotnet new grpc -o Server --no-restore
mv Server/Protos Protos && pushd Protos
dotnet new classlib --no-restore
rm Class1.cs
dotnet add package Grpc.AspNetCore --no-restore
popd

# reference the server stub from the server
pushd Server && dotnet add reference ../Protos && popd
 
# create a console client that talks to the server
dotnet new console -o Client --no-restore && pushd Client
dotnet add package Grpc.Net.Client --no-restore
dotnet add reference ../Protos && popd

echo "Follow the instructions at https://docs.microsoft.com/en-us/aspnet/core/grpc/?view=aspnetcore-5.0#call-grpc-services-with-a-net-client-1"

# demo using the out-of-box template
dotnet new grpc -o Simple --no-restore

echo "To use the servers without https, you might need to: https://docs.microsoft.com/en-us/aspnet/core/grpc/troubleshoot?view=aspnetcore-5.0#unable-to-start-aspnet-core-grpc-app-on-macos" 

bazel run //:gazelle
```
