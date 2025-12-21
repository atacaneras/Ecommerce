   Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
   
   # =====================================
    #  E-Ticaret Mikroservis Başlatıcı
    # =====================================

    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  E-Ticaret Mikroservis Başlatıcı" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""

    # Compose komutunu otomatik algıla
    function Get-DockerComposeCommand {
        if (Get-Command "docker-compose" -ErrorAction SilentlyContinue) {
            return "docker-compose"
        }
        elseif (Get-Command "docker" -ErrorAction SilentlyContinue) {
            return "docker compose"
        }
        else {
            Write-Host "Docker Compose yüklü değil!" -ForegroundColor Red
            exit 1
        }
    }

    $composeCmd = Get-DockerComposeCommand
    Write-Host "Kullanılan Compose Komutu: $composeCmd" -ForegroundColor Yellow
    Write-Host ""

    # Frontend klasörü kontrolü
    if (-Not (Test-Path "frontend")) {
        Write-Host "⚠️  frontend klasörü bulunamadı!" -ForegroundColor Yellow
        Write-Host "Lütfen FRONTEND_SETUP.md dosyasını okuyun ve frontend klasörünü oluşturun." -ForegroundColor Yellow
        Write-Host ""
        exit 1
    }

    if (-Not (Test-Path "frontend/package.json")) {
        Write-Host "⚠️  frontend/package.json bulunamadı!" -ForegroundColor Yellow
        Write-Host "Lütfen FRONTEND_SETUP.md dosyasını okuyun." -ForegroundColor Yellow
        exit 1
    }

    # Servisleri başlat
    Write-Host "[2] Servisler inşa ediliyor ve başlatılıyor..." -ForegroundColor Yellow
    $buildOutput = & $composeCmd up -d

    if ($LASTEXITCODE -ne 0) {
        Write-Host "✗ Servisler başlatılamadı!" -ForegroundColor Red
        Write-Host $buildOutput
        exit 1
    }

    Write-Host "    ✓ Servisler başlatıldı" -ForegroundColor Green
    Write-Host ""

    # Bekle
    Write-Host "[3] Servislerin başlaması bekleniyor (15 saniye)..." -ForegroundColor Yellow
    for ($i = 1; $i -le 15; $i++) {
        Write-Host -NoNewline "."
        Start-Sleep -Seconds 1
    }
    Write-Host " Tamam!" -ForegroundColor Green
    Write-Host ""

    # Sağlık kontrolü
    $services = @(
        @{Display="Frontend"; Url="http://localhost:3001"},
        @{Display="Sipariş Servisi"; Url="http://localhost:5001/swagger"},
        @{Display="Stok Servisi"; Url="http://localhost:5002/swagger"},
        @{Display="Bildirim Servisi"; Url="http://localhost:5003/swagger"},
        @{Display="Onay Servisi"; Url="http://localhost:5004/swagger"},
        @{Display="Fatura Servisi"; Url="http://localhost:5005/swagger"},
        @{Display="Auth Servisi"; Url="http://localhost:5000/swagger"},
        @{Display="RabbitMQ Yönetim"; Url="http://localhost:15672"}
    )

    Write-Host "[4] Sağlık Kontrolü:" -ForegroundColor Yellow

    $healthy = @()

    foreach ($s in $services) {
        try {
            Invoke-WebRequest -Uri $s.Url -TimeoutSec 5 -UseBasicParsing -ErrorAction SilentlyContinue | Out-Null
            Write-Host "    ✓ $($s.Display) sağlıklı" -ForegroundColor Green
            $healthy += $s
        }
        catch {
            Write-Host "    ⏳ $($s.Display) başlanıyor..." -ForegroundColor Yellow
        }
    }

    Write-Host ""
    Write-Host "========================================"
    Write-Host "  Container Durumu"
    Write-Host "========================================"
    & $composeCmd ps

    Write-Host ""
    Write-Host "📋 Erişim Noktaları:"
    Write-Host ""
    foreach ($s in $services) {
        Write-Host "  🔗 $($s.Display)" -ForegroundColor Cyan
        Write-Host "     $($s.Url)" -ForegroundColor White
    }

    Write-Host ""
    Write-Host "🔗 Tarayıcı açılıyor..." -ForegroundColor Yellow
    foreach ($s in $healthy) {
        $proc = Start-Process $s.Url -PassThru -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 500
    }

    Write-Host ""
    Write-Host "========================================"
    Write-Host " 📊 LOGLAR (durdurmak için Ctrl+C)"
    Write-Host "========================================"
    Write-Host ""

    & $composeCmd logs -f