var builder = DistributedApplication.CreateBuilder(args);

// Add PostgreSQL database
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .AddDatabase("llmgateway-db");

// Add the API project with database connection
var api = builder.AddProject<Projects.LLMGateway_Api>("llmgateway-api")
    .WithReference(postgres);

builder.Build().Run();
