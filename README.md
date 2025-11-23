# Beymen Case Study

## Teknolojiler

- **.NET 8.0**
- **PostgreSQL 15**
- **RabbitMQ 3** (Management Plugin ile)
- **Entity Framework Core 8.0**
- **Docker & Docker Compose**
- **Polly** (Retry Policies)
- **Swagger / OpenAPI**

---

## Kurulum ve Çalıştırma

### 1️⃣ Projeyi İndirin
```bash
git clone <repository-url>
cd BeymenCase
```
2️⃣ Docker Compose ile Çalıştırın
```bash
docker-compose up --build
```
veya PowerShell kullanarak:

Proje klasörü içinde start.ps1 scriptini çalıştırabilirsiniz.

Bu script tüm containerları ayağa kaldırır ve ilgili Swagger sayfalarını otomatik açar:

```bash
powershell
.\start.ps1
