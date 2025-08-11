# DudeBank - Payment Processing System (.NET Implementation)

Sistema de intermediação de pagamentos desenvolvido para a **Rinha de Backend 2025** 🐔 🚀

**Implementação em .NET 9 com AOT (Ahead-of-Time) compilation**

## 🏗️ Stack / Arquitetura

- .NET 9 with Native AOT
- In-memory database (efficient bit-packed storage)
- Unix Domain Sockets for IPC
- Nginx Load Balancer
- Minimal APIs with source generation

```
┌─────────────┐    ┌─────────────┐    ┌─────────────┐
│   Nginx     │───▶│   API 1     │───▶│  Database   │
│Load Balancer│    │   API 2     │    │   Server    │
│ (least_conn)│    │ (Unix Sock) │    │(Unix Socket)│
└─────────────┘    └─────────────┘    └─────────────┘
                          │
                          ▼
                   ┌─────────────┐
                   │Payment Queue│
                   │(In-Memory)  │
                   └─────────────┘
```

## 🚀 Key Features

### Native AOT Compilation
- **Faster startup**: Cold start in milliseconds
- **Lower memory usage**: Reduced memory footprint
- **Smaller binary size**: Self-contained executable
- **No JIT overhead**: Direct native code execution

### High-Performance Architecture
- **Unix Domain Sockets**: Efficient IPC communication
- **Bit-packed storage**: Memory-efficient data compression
- **Async batch processing**: Queue-based payment processing
- **Connection pooling**: Optimized HTTP client usage
- **Lock-free queues**: Concurrent data structures

### JSON Source Generation
- **AOT-compatible serialization**: Using System.Text.Json source generators
- **Compile-time optimization**: No reflection at runtime
- **Type-safe serialization**: Strongly-typed JSON handling

## 📁 Project Structure

```
backend/
├── Program.cs                    # Main API server
├── Configuration/
│   ├── AppConfig.cs             # Application configuration
│   └── AppJsonContext.cs        # JSON serialization context
├── Models/
│   └── PaymentModels.cs         # Data models and DTOs
├── Services/
│   ├── QueueService.cs          # Generic concurrent queue
│   ├── DatabaseClient.cs        # Database communication client
│   ├── PaymentCommand.cs        # Payment processing service
│   ├── PaymentQuery.cs          # Payment query service
│   └── PaymentProcessorRouter.cs # Payment processor routing
├── Database/
│   ├── MemoryStore.cs           # Bit-packed in-memory storage
│   ├── DatabaseService.cs       # Database operations
│   ├── DatabaseServer.cs        # Database server class
│   └── Program.cs               # Database server program
└── Dockerfile                   # Multi-stage Docker build
```

## 🔧 Building and Running

### Prerequisites
- .NET 9 SDK
- Docker and Docker Compose
- Linux/macOS for Unix sockets (Windows with WSL2)

### Local Development
```bash
# Build the main API
cd backend
dotnet build

# Build the database server
cd Database
dotnet build

# Run locally (requires Unix socket support)
dotnet run --project backend
dotnet run --project Database
```

### Docker Deployment
```bash
# Build all containers
docker compose build

# Start the application
docker compose up

# Test the API
curl -X POST http://localhost:9999/payments \
  -H "Content-Type: application/json" \
  -d '{"correlationId":"test-123","amount":100.50}'

# Check payment summary
curl http://localhost:9999/payments-summary
```

## 🏃‍♂️ Performance Benefits

### Native AOT Advantages
1. **Startup Time**: ~10-50ms vs ~1-2s with JIT
2. **Memory Usage**: ~30-50% reduction in memory footprint
3. **Binary Size**: Self-contained, no .NET runtime required
4. **Cold Starts**: Ideal for serverless and containerized workloads

### Memory Optimization
- **Bit-packed storage**: Stores timestamp + amount in 32 bits
- **Zero-allocation queues**: Reuses objects to minimize GC pressure
- **Struct-based models**: Value types for better memory locality

### I/O Performance
- **Unix Domain Sockets**: 2-3x faster than TCP for local communication
- **HTTP/1.1 keep-alive**: Connection reuse for external requests
- **Async/await**: Non-blocking I/O operations

## 🔍 Technical Implementation Details

### Memory Store Bit Packing
The system uses efficient bit packing to store payment data:
- **11 bits**: Amount in cents (supports up to $20.47)
- **21 bits**: Relative timestamp (supports ~35 minutes range)
- **Total**: 32 bits per payment record

### JSON Source Generation
All JSON serialization uses compile-time source generation:
```csharp
[JsonSerializable(typeof(PaymentRequest))]
[JsonSerializable(typeof(PaymentSummary))]
public partial class AppJsonContext : JsonSerializerContext { }
```

### Unix Socket Configuration
```csharp
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenUnixSocket(config.Server.SocketPath);
});
```

## 📊 API Endpoints

### POST /payments
Enqueue a payment for processing
```json
{
  "correlationId": "payment-123",
  "amount": 150.75
}
```

### GET /payments-summary
Get payment processing summary
```json
{
  "default": {
    "totalRequests": 1500,
    "totalAmount": 45250.80
  },
  "fallback": {
    "totalRequests": 25,
    "totalAmount": 1250.00
  }
}
```

### DELETE /admin/purge
Clear all payment data (admin endpoint)

## 🐳 Docker Configuration

### Multi-stage Build
- **Build stage**: .NET SDK with AOT compilation
- **Runtime stage**: Runtime-deps image (no .NET runtime needed)
- **Security**: Non-root user execution
- **Size optimization**: Minimal base image

### Resource Limits
- **API instances**: 155MB RAM, 0.65 CPU
- **Database**: 50MB RAM, 0.05 CPU  
- **Nginx**: 40MB RAM, 0.2 CPU

## 🔒 Security Features

- **Non-root containers**: All processes run as unprivileged users
- **Resource limits**: CPU and memory constraints
- **Socket permissions**: Proper Unix socket access controls
- **Input validation**: Type-safe deserialization

## 🚀 Production Considerations

### Scaling
- **Horizontal scaling**: Multiple API instances behind load balancer
- **Database separation**: Dedicated database server instance
- **Connection pooling**: Efficient resource utilization

### Monitoring
- **Structured logging**: JSON-formatted application logs
- **Health checks**: Built-in health monitoring endpoints
- **Metrics**: Performance counters and timing information

### Deployment
- **Blue-green deployment**: Zero-downtime updates
- **Container orchestration**: Kubernetes/Docker Swarm ready
- **Configuration**: Environment variable based configuration

---

This .NET implementation provides significant performance improvements over traditional runtime-compiled applications while maintaining full compatibility with the original API specification.
