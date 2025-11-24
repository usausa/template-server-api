// ReSharper disable StringLiteralTypo
var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Template_ApiServer_Host>("apiserver");

builder.Build().Run();
