# 🐳 Docker Setup – WebAPIShop

## 📋 Prerequisites
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running

---

## 🚀 Quick Start

```bash
# Clone the project and navigate to the root folder
docker-compose up --build
```

That's it! Docker will automatically:
- Start SQL Server and initialize the database with all tables and data
- Start Redis
- Start Kafka + Kafka UI
- Build and start the API
- Start the Kafka Consumer (Worker Service)

---

## 🌐 Services & Ports

| Service | URL |
|---|---|
| API (Swagger) | http://localhost:8080/swagger |
| Kafka UI | http://localhost:8090 |
| Redis | localhost:6379 |
| SQL Server | localhost:1433 |

---

## 🔑 Credentials

| Service | Username | Password |
|---|---|---|
| SQL Server | `sa` | `Your_password123` |
| Redis | - | `yourpassword` |

---

## 🛠️ Local Development (Visual Studio)

When running from Visual Studio, create `WebAPIShop/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "new_connection": "YOUR_LOCAL_DB_CONNECTION_STRING",
    "Redis": "localhost:6379,password=yourpassword"
  },
  "Jwt": {
    "Key": "YOUR_SECRET_KEY_MIN_32_CHARS"
  },
  "Kafka": {
    "BootstrapServers": "localhost:9093"
  }
}
```

> ⚠️ This file is in `.gitignore` and should never be committed.

---

## 🛑 Stop & Cleanup

```bash
# Stop all containers
docker-compose down

# Stop and remove all data (volumes)
docker-compose down -v
```
