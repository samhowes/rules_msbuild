# //tests/examples/HelloWeb

This package tests the loading of configuration files using the standard WebApplication template. `Program.cs` configures everything with the defaults and attempts to print out a value from its `appsettings.json`. This test makes sure the running executable has access to its configuration files.

Given the default setup of Asp.Net:
```csharp
Host.CreateDefaultBuilder(args)
   .ConfigureWebHostDefaults(webBuilder => 
        { webBuilder.UseStartup<Startup>(); });
```

It appears the WebHost loads the appsettings.json from the directory of the startup assembly, so the starting directory does not appear to matter. I.e. it is totally fine that bazel starts the process in `<target_name>.runfiles/<workspace_name>`.

