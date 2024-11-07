using OrdergRPCClient;
using ProductgRPCServer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services.AddGrpcClient<ProductGrpc.ProductGrpcClient>(options =>
{
    options.Address = new Uri("http://localhost:5050");
});
builder.Services.AddScoped<OrderServiceGrpcClient>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer(); builder.Services.AddSwaggerGen();
var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.MapControllers();
app.Run();


