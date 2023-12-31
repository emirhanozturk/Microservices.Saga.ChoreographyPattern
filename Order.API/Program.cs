using MassTransit;
using Microsoft.EntityFrameworkCore;
using Order.API.Consumers;
using Order.API.Models;
using Order.API.Models.Contexts;
using Order.API.ViewModels;
using Shared;
using Shared.Events;
using Shared.Messages;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMassTransit(configurator =>
{
    configurator.AddConsumer<PaymentCompletedEventConsumer>();
    configurator.AddConsumer<PaymentFailedEventConsumer>();
    configurator.AddConsumer<StockNotReservedEventConsumer>();
    configurator.UsingRabbitMq((context, _configure) =>
    {
        _configure.Host(builder.Configuration["RabbitMQ"]);
        _configure.ReceiveEndpoint(RabbitMQSettings.Order_PaymentCompletedEventQueue,e=>
        {
            e.ConfigureConsumer<PaymentCompletedEventConsumer>(context);
        });
        _configure.ReceiveEndpoint(RabbitMQSettings.Order_PaymentFailedEventQueue,e=>
        {
            e.ConfigureConsumer<PaymentFailedEventConsumer>(context);
        });
        _configure.ReceiveEndpoint(RabbitMQSettings.Order_StockNotReservedEventQueue,e=>
        {
            e.ConfigureConsumer<StockNotReservedEventConsumer>(context);
        });
    });
});

builder.Services.AddDbContext<OrderAPIDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("MSSQLServer")));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapPost("/create-order",async (CreateOrderVM model,OrderAPIDbContext context, IPublishEndpoint endpoint) =>
{
    Order.API.Models.Order order = new()
    {
        BuyerId = Guid.TryParse(model.BuyerId,out Guid _buyerId) ? _buyerId : Guid.NewGuid(),
        Id = Guid.NewGuid(),
        OrderItems = model.OrderItems.Select(p=> new OrderItem
        {
            Price = p.Price,
            Count = p.Count,
            ProductId = Guid.Parse(p.ProductId)
        }).ToList(),
        CreatedDate = DateTime.UtcNow,
        Status = Order.API.Enums.OrderStatus.Suspend,
        TotalPrice = model.OrderItems.Sum(p => p.Price * p.Count)
    };

    await context.Orders.AddAsync(order);
    await context.SaveChangesAsync();

    OrderCreatedEvent orderCreatedEvent = new()
    {
        BuyerId = order.BuyerId,
        OrderId = order.Id,
        TotalPrice = order.TotalPrice,
        OrderItems = order.OrderItems.Select(p => new OrderItemMessage
        {
            Count = p.Count,
            Price = p.Price,
            ProductId = p.ProductId,
        }).ToList()


    };

    await endpoint.Publish(orderCreatedEvent);
});



app.Run();
