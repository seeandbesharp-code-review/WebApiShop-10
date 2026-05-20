namespace Services;

using AutoMapper;
using Confluent.Kafka;
using DTOs;
using Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Repository;
using System.Text.Json;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<OrderService> _logger;
    private readonly IConfiguration _configuration;

    public OrderService(IOrderRepository orderRepository, IMapper mapper, IProductRepository productRepository, ILogger<OrderService> logger, IConfiguration configuration)
    {
        _orderRepository = orderRepository;
        _mapper = mapper;
        _productRepository = productRepository;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<OrderDTO> AddOrder(OrderDTO oreder)
    {
        double sum = 0;
        foreach (var item in oreder.OrderItems)
        {
            Product product = await _productRepository.GetProductById(item.ProductID);
            if (product != null)
                sum += product.Price * item.Quantity;
        }
        if (sum != oreder.OrderSum)
        {
            _logger.LogWarning("Order sum mismatch for user {UserId}. Client sent: {ClientSum}, Server calculated: {ServerSum} 😖", oreder.UserId, oreder.OrderSum, sum);
            oreder = oreder with { OrderSum = sum };
        }
        OrderDTO createdOrder = _mapper.Map<Order, OrderDTO>(await _orderRepository.AddOrder(_mapper.Map<OrderDTO, Order>(oreder)));
        await SendToKafkaAsync(createdOrder);
        return createdOrder;
    }

    public async Task<OrderDTO> GetOrderById(int id)
    {
        return _mapper.Map<Order, OrderDTO>(await _orderRepository.GetOrderById(id));
    }

    public async Task<List<OrderDTO>> GetAllOrders()
    {
        return _mapper.Map<List<Order>, List<OrderDTO>>(await _orderRepository.GetAllOrders());
    }

    private async Task SendToKafkaAsync(OrderDTO order)
    {
        try
        {
            var config = new ProducerConfig
            {
                BootstrapServers = _configuration["Kafka:BootstrapServers"],
                MessageTimeoutMs = 3000,
                RequestTimeoutMs = 3000
            };
            var topic = _configuration["Kafka:Topic"];
            using var producer = new ProducerBuilder<Null, string>(config).Build();
            var message = JsonSerializer.Serialize(order);
            await producer.ProduceAsync(topic, new Message<Null, string> { Value = message });
            _logger.LogInformation("Order {OrderId} sent to Kafka topic '{Topic}'", order.OrderId, topic);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to send order to Kafka: {Message}", ex.Message);
        }
    }
}
